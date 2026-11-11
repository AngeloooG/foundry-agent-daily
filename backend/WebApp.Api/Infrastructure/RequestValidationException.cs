namespace WebApp.Api.Infrastructure;

/// <summary>
/// Thrown when client input is invalid. Mapped to HTTP 400 by <see cref="ApiExceptionHandler"/>,
/// which returns the individual <see cref="Errors"/> (they describe the user's own input, so they
/// are safe to show in every environment and tell the user what to fix).
/// </summary>
public sealed class RequestValidationException : Exception
{
    public IReadOnlyList<string> Errors { get; }

    public RequestValidationException(IReadOnlyList<string> errors)
        : base(errors.Count == 1 ? errors[0] : $"{errors.Count} validation errors: {string.Join("; ", errors)}")
    {
        Errors = errors;
    }

    public RequestValidationException(string error) : this([error])
    {
    }
}
