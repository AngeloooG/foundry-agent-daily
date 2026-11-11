using System.ClientModel;
using System.Net;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using WebApp.Api.Models;

namespace WebApp.Api.Infrastructure;

/// <summary>
/// Single place that turns exceptions into RFC 7807 ProblemDetails responses.
/// Replaces the per-endpoint try/catch blocks.
/// </summary>
/// <remarks>
/// Upstream failures from Foundry (401/403/5xx) map to 502: they are a server-side problem
/// (e.g. missing RBAC on the managed identity), not an expired user session, so returning 401
/// would push the browser into a pointless re-login loop. Details are sanitized outside
/// Development by <see cref="ErrorResponseFactory"/>.
/// </remarks>
public sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // Client disconnected: nothing to send and not an error.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            logger.LogDebug("Request {Path} cancelled by the client", httpContext.Request.Path);
            return true;
        }

        var status = MapStatusCode(exception);
        if (status >= 500)
        {
            logger.LogError(exception, "Unhandled error on {Method} {Path} -> {Status}", httpContext.Request.Method, httpContext.Request.Path, status);
        }
        else
        {
            logger.LogWarning("Request {Method} {Path} rejected with {Status}: {ExceptionType}", httpContext.Request.Method, httpContext.Request.Path, status, exception.GetType().Name);
        }

        if (httpContext.Response.HasStarted)
        {
            return true;
        }

        var problem = CreateProblemDetails(exception, status, environment.IsDevelopment());
        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem
        });
    }

    /// <summary>Maps an exception to the HTTP status returned to the client.</summary>
    public static int MapStatusCode(Exception exception) => exception switch
    {
        RequestValidationException => StatusCodes.Status400BadRequest,
        UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
        ClientResultException { Status: 404 } => StatusCodes.Status404NotFound,
        ClientResultException { Status: 429 } => StatusCodes.Status503ServiceUnavailable,
        ClientResultException => StatusCodes.Status502BadGateway,
        HttpRequestException { StatusCode: HttpStatusCode.NotFound } => StatusCodes.Status404NotFound,
        HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests } => StatusCodes.Status503ServiceUnavailable,
        HttpRequestException => StatusCodes.Status502BadGateway,
        _ => StatusCodes.Status500InternalServerError
    };

    /// <summary>Builds the ProblemDetails body (also used for SSE error events).</summary>
    public static ProblemDetails CreateProblemDetails(Exception exception, int status, bool isDevelopment)
    {
        var error = ErrorResponseFactory.CreateFromException(exception, status, isDevelopment);
        var problem = new ProblemDetails
        {
            Status = status,
            Title = error.Title,
            Detail = error.Detail
        };

        if (exception is RequestValidationException validation)
        {
            problem.Detail = string.Join(" ", validation.Errors);
            problem.Extensions["errors"] = validation.Errors;
        }

        if (error.Extensions is not null)
        {
            foreach (var (key, value) in error.Extensions)
            {
                problem.Extensions[key] = value;
            }
        }

        return problem;
    }
}
