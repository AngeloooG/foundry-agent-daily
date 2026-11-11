namespace WebApp.Api.Infrastructure;

/// <summary>
/// Thrown when a file exceeds <c>ChatLimits:MaxDownloadBytes</c>. Mapped to HTTP 413 by
/// <see cref="ApiExceptionHandler"/>. The message states only the configured limit (never the
/// file's real size). It is raised after the ownership and file-association checks, so a caller
/// who does not own the conversation always receives 404 and learns nothing about the file.
/// </summary>
public sealed class FileTooLargeException(long maxBytes)
    : Exception($"The file exceeds the maximum download size ({maxBytes / (1024.0 * 1024.0):0.#} MB).")
{
    public long MaxBytes { get; } = maxBytes;
}
