using System.Text;
using WebApp.Api.Infrastructure;
using WebApp.Api.Models;
using WebApp.Api.Options;

namespace WebApp.Api.Services;

/// <summary>Image ready to upload to the Foundry Files API.</summary>
public sealed record ImageAttachment(string MediaType, byte[] Bytes);

/// <summary>PDF sent to the model as file input.</summary>
public sealed record PdfAttachment(string FileName, string MediaType, byte[] Bytes);

/// <summary>Validated attachments of one chat message.</summary>
public sealed record PreparedAttachments(
    IReadOnlyList<ImageAttachment> Images,
    IReadOnlyList<string> InlineTexts,
    IReadOnlyList<PdfAttachment> Pdfs)
{
    public static readonly PreparedAttachments None = new([], [], []);

    public bool IsEmpty => Images.Count == 0 && InlineTexts.Count == 0 && Pdfs.Count == 0;
}

/// <summary>
/// Validates and decodes chat attachments (base64 data URIs) <b>before</b> anything is uploaded or a
/// conversation is created, so an invalid request has no side effects and returns HTTP 400.
/// </summary>
/// <remarks>
/// Pure functions (no I/O) so every rule is unit-testable. Checks: count, per-item and total size
/// (estimated from the base64 length before decoding, to avoid large allocations), allowed MIME type,
/// declared-vs-data-URI MIME type, binary signature (magic bytes) for images and PDFs, valid UTF-8
/// for text documents, and a cap on characters inlined into the prompt.
/// </remarks>
public static class AttachmentProcessor
{
    internal static readonly IReadOnlySet<string> AllowedImageTypes =
        new HashSet<string>(StringComparer.Ordinal) { "image/png", "image/jpeg", "image/jpg", "image/gif", "image/webp" };

    /// <summary>Text documents are inlined into the prompt (the Responses API only accepts PDF as file input).</summary>
    internal static readonly IReadOnlySet<string> TextDocumentTypes =
        new HashSet<string>(StringComparer.Ordinal) { "text/plain", "text/markdown", "text/csv", "application/json", "application/xml", "text/xml" };

    internal static readonly IReadOnlySet<string> PdfTypes =
        new HashSet<string>(StringComparer.Ordinal) { "application/pdf" };

    private const int MaxFileNameLength = 120;
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static PreparedAttachments Prepare(
        IReadOnlyList<string>? imageDataUris,
        IReadOnlyList<FileAttachment>? files,
        ChatLimitsOptions limits)
    {
        var imageCount = imageDataUris?.Count ?? 0;
        var fileCount = files?.Count ?? 0;
        if (imageCount == 0 && fileCount == 0)
        {
            return PreparedAttachments.None;
        }

        var errors = new List<string>();
        if (imageCount > limits.MaxImageCount)
        {
            errors.Add($"Too many images ({imageCount}); maximum {limits.MaxImageCount} allowed.");
        }
        if (fileCount > limits.MaxFileCount)
        {
            errors.Add($"Too many files ({fileCount}); maximum {limits.MaxFileCount} allowed.");
        }
        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors);
        }

        var images = new List<ImageAttachment>();
        var inlineTexts = new List<string>();
        var pdfs = new List<PdfAttachment>();
        long totalBytes = 0;
        var inlineChars = 0;

        for (var i = 0; i < imageCount; i++)
        {
            var label = $"Image {i + 1}";
            if (!TryDecodeDataUri(imageDataUris![i], limits.MaxImageBytes, out var mediaType, out var bytes, out var error))
            {
                errors.Add($"{label}: {error}");
                continue;
            }
            if (!AllowedImageTypes.Contains(mediaType))
            {
                errors.Add($"{label}: unsupported type '{mediaType}'. Allowed: PNG, JPEG, GIF, WebP.");
                continue;
            }
            if (!HasExpectedSignature(mediaType, bytes))
            {
                errors.Add($"{label}: content does not match type '{mediaType}'.");
                continue;
            }

            totalBytes += bytes.Length;
            images.Add(new ImageAttachment(mediaType, bytes));
        }

        for (var i = 0; i < fileCount; i++)
        {
            var file = files![i];
            var fileName = SanitizeFileName(file.FileName);
            var label = $"File {i + 1} ({fileName})";

            if (!TryDecodeDataUri(file.DataUri, limits.MaxFileBytes, out var mediaType, out var bytes, out var error))
            {
                errors.Add($"{label}: {error}");
                continue;
            }
            if (!string.Equals(mediaType, file.MimeType?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"{label}: declared type '{file.MimeType}' does not match data type '{mediaType}'.");
                continue;
            }

            if (PdfTypes.Contains(mediaType))
            {
                if (!HasExpectedSignature(mediaType, bytes))
                {
                    errors.Add($"{label}: content is not a valid PDF.");
                    continue;
                }
                totalBytes += bytes.Length;
                pdfs.Add(new PdfAttachment(fileName, mediaType, bytes));
            }
            else if (TextDocumentTypes.Contains(mediaType))
            {
                string text;
                try
                {
                    text = StrictUtf8.GetString(bytes);
                }
                catch (DecoderFallbackException)
                {
                    errors.Add($"{label}: not valid UTF-8 text.");
                    continue;
                }

                inlineChars += text.Length;
                if (inlineChars > limits.MaxInlineTextChars)
                {
                    errors.Add($"{label}: text documents exceed {limits.MaxInlineTextChars:N0} characters in total.");
                    continue;
                }

                totalBytes += bytes.Length;
                inlineTexts.Add($"\n\n--- Content of {fileName} ---\n{text}\n--- End of {fileName} ---\n");
            }
            else
            {
                errors.Add($"{label}: unsupported type '{mediaType}'. Allowed: PDF, TXT, MD, CSV, JSON, XML.");
            }
        }

        if (totalBytes > limits.MaxTotalAttachmentBytes)
        {
            errors.Add($"Attachments total {ToMegabytes(totalBytes):F1}MB; maximum {ToMegabytes(limits.MaxTotalAttachmentBytes):F1}MB per message.");
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors);
        }

        return new PreparedAttachments(images, inlineTexts, pdfs);
    }

    /// <summary>
    /// Parses <c>data:&lt;mediatype&gt;[;param];base64,&lt;data&gt;</c>. Rejects non-base64 URIs and
    /// payloads larger than <paramref name="maxBytes"/> before decoding them.
    /// </summary>
    internal static bool TryDecodeDataUri(string? dataUri, long maxBytes, out string mediaType, out byte[] bytes, out string error)
    {
        mediaType = string.Empty;
        bytes = [];
        error = string.Empty;

        if (string.IsNullOrEmpty(dataUri) || !dataUri.StartsWith("data:", StringComparison.Ordinal))
        {
            error = "invalid format (must be a data URI).";
            return false;
        }

        var commaIndex = dataUri.IndexOf(',', StringComparison.Ordinal);
        if (commaIndex < 0)
        {
            error = "malformed data URI.";
            return false;
        }

        var header = dataUri.AsSpan(5, commaIndex - 5).ToString().Split(';', StringSplitOptions.TrimEntries);
        mediaType = header[0].ToLowerInvariant();
        if (mediaType.Length == 0 || !header.Skip(1).Contains("base64", StringComparer.OrdinalIgnoreCase))
        {
            error = "malformed data URI (expected ';base64').";
            return false;
        }

        var base64Length = dataUri.Length - commaIndex - 1;
        var estimatedBytes = (long)base64Length * 3 / 4;
        if (estimatedBytes > maxBytes + 2)
        {
            error = $"size {ToMegabytes(estimatedBytes):F1}MB exceeds maximum {ToMegabytes(maxBytes):F1}MB.";
            return false;
        }

        try
        {
            bytes = Convert.FromBase64String(dataUri[(commaIndex + 1)..]);
        }
        catch (FormatException)
        {
            error = "invalid Base64 encoding.";
            return false;
        }

        if (bytes.Length > maxBytes)
        {
            error = $"size {ToMegabytes(bytes.Length):F1}MB exceeds maximum {ToMegabytes(maxBytes):F1}MB.";
            return false;
        }

        return true;
    }

    /// <summary>Magic-byte check so a renamed or mislabeled file is rejected.</summary>
    internal static bool HasExpectedSignature(string mediaType, ReadOnlySpan<byte> bytes) => mediaType switch
    {
        "image/png" => bytes.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
        "image/jpeg" or "image/jpg" => bytes.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]),
        "image/gif" => bytes.StartsWith("GIF87a"u8) || bytes.StartsWith("GIF89a"u8),
        "image/webp" => bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8),
        "application/pdf" => bytes.StartsWith("%PDF-"u8),
        _ => true
    };

    /// <summary>
    /// The file name is user-controlled and is written into the prompt and into error messages:
    /// strip control characters (no forged "--- End of ---" markers via newlines) and cap the length.
    /// </summary>
    internal static string SanitizeFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "attachment";
        }

        var cleaned = new string(fileName.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (cleaned.Length == 0)
        {
            return "attachment";
        }

        return cleaned.Length <= MaxFileNameLength ? cleaned : cleaned[..MaxFileNameLength];
    }

    private static double ToMegabytes(long bytes) => bytes / (1024.0 * 1024.0);
}
