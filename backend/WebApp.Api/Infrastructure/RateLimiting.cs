using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using WebApp.Api.Options;
using WebApp.Api.Security;

namespace WebApp.Api.Infrastructure;

public static class RateLimitPolicies
{
    /// <summary>All authenticated API calls (per user).</summary>
    public const string Api = "api";

    /// <summary>Chat requests, which consume model tokens (per user).</summary>
    public const string Chat = "chat";

    /// <summary>
    /// Per-user fixed windows. Partitioned by the hashed user key (falls back to the client IP for
    /// anonymous callers). State is in-memory per replica: with N replicas the effective limit is N x.
    /// For a shared, cross-replica quota use Azure API Management (llm-token-limit) — see ARCHITECTURE.
    /// </summary>
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                var problemDetails = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
                await problemDetails.TryWriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context.HttpContext,
                    ProblemDetails =
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too Many Requests",
                        Detail = "You've made too many requests. Please wait a moment and try again."
                    }
                });
            };

            options.AddPolicy(Api, context => CreatePartition(context, limits => limits.ApiRequestsPerMinute));
            options.AddPolicy(Chat, context => CreatePartition(context, limits => limits.ChatRequestsPerMinute));
        });

        return services;
    }

    private static RateLimitPartition<string> CreatePartition(HttpContext context, Func<ChatLimitsOptions, int> permitLimit)
    {
        var limits = context.RequestServices.GetRequiredService<IOptions<ChatLimitsOptions>>().Value;
        var partitionKey = UserIdentity.GetOwnerKey(context.User)
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit(limits),
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    }
}
