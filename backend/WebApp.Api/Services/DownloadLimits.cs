using WebApp.Api.Infrastructure;

namespace WebApp.Api.Services;

/// <summary>
/// Enforces <c>ChatLimits:MaxDownloadBytes</c> without loading files that are already known to be
/// too large: first by the size Foundry reports (file metadata or Content-Length), and, when the
/// size is unknown, by reading at most <c>maxBytes + 1</c> bytes.
/// </summary>
public static class DownloadLimits
{
    /// <summary>Throws <see cref="FileTooLargeException"/> when a known size exceeds the limit. Unknown (null) sizes pass.</summary>
    public static void EnsureWithinLimit(long? knownSize, long maxBytes)
    {
        if (knownSize is long size && size > maxBytes)
        {
            throw new FileTooLargeException(maxBytes);
        }
    }

    /// <summary>
    /// Reads the whole stream if it fits in <paramref name="maxBytes"/>; otherwise stops after
    /// <c>maxBytes + 1</c> bytes and throws <see cref="FileTooLargeException"/>.
    /// </summary>
    public static async Task<byte[]> ReadBoundedAsync(Stream source, long maxBytes, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        long total = 0;

        while (true)
        {
            // Never request more than one byte past the limit.
            var toRead = (int)Math.Min(chunk.Length, maxBytes + 1 - total);
            var read = await source.ReadAsync(chunk.AsMemory(0, toRead), cancellationToken);
            if (read == 0)
            {
                break;
            }

            total += read;
            if (total > maxBytes)
            {
                throw new FileTooLargeException(maxBytes);
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
