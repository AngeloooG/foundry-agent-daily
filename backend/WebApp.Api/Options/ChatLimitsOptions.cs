using System.ComponentModel.DataAnnotations;

namespace WebApp.Api.Options;

/// <summary>
/// Input and usage limits for the chat API (configuration section <c>ChatLimits</c>).
/// </summary>
/// <remarks>
/// These limits protect cost (tokens) and availability (memory / DoS). Attachments travel as
/// base64 inside the JSON body (+33%), so <see cref="MaxRequestBodyBytes"/> must stay above
/// <see cref="MaxTotalAttachmentBytes"/> * 4/3. Rate limits are per user and per replica
/// (in-memory), so the effective limit is multiplied by the number of replicas.
/// </remarks>
public sealed class ChatLimitsOptions
{
    public const string SectionName = "ChatLimits";

    [Range(1, 100_000)]
    public int MaxMessageLength { get; set; } = 16_000;

    [Range(0, 20)]
    public int MaxImageCount { get; set; } = 5;

    [Range(typeof(long), "1", "52428800")]
    public long MaxImageBytes { get; set; } = 5 * 1024 * 1024;

    [Range(0, 20)]
    public int MaxFileCount { get; set; } = 10;

    [Range(typeof(long), "1", "52428800")]
    public long MaxFileBytes { get; set; } = 20 * 1024 * 1024;

    /// <summary>Decoded bytes across all images and files of one message.</summary>
    [Range(typeof(long), "1", "52428800")]
    public long MaxTotalAttachmentBytes { get; set; } = 20 * 1024 * 1024;

    /// <summary>Characters of text-based documents inlined into the prompt (token cost guard).</summary>
    [Range(1, 2_000_000)]
    public int MaxInlineTextChars { get; set; } = 200_000;

    /// <summary>Maximum HTTP body for POST /api/chat/stream.</summary>
    [Range(typeof(long), "1024", "104857600")]
    public long MaxRequestBodyBytes { get; set; } = 30_000_000;

    /// <summary>Maximum bytes served by the file download endpoint.</summary>
    [Range(typeof(long), "1024", "209715200")]
    public long MaxDownloadBytes { get; set; } = 50 * 1024 * 1024;

    [Range(1, 10_000)]
    public int ChatRequestsPerMinute { get; set; } = 20;

    [Range(1, 10_000)]
    public int ApiRequestsPerMinute { get; set; } = 120;
}
