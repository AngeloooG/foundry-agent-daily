using Microsoft.AspNetCore.HttpOverrides;

namespace WebApp.Api.Security;

/// <summary>Configuration section <c>SecurityHeaders</c> (per environment).</summary>
public sealed class SecurityHeadersOptions
{
    public const string SectionName = "SecurityHeaders";

    /// <summary>Full CSP override. Empty = policy built by <see cref="ContentSecurityPolicyBuilder"/>.</summary>
    public string? ContentSecurityPolicy { get; set; }

    /// <summary>Send Content-Security-Policy-Report-Only instead (to trial a change without breaking users).</summary>
    public bool ReportOnly { get; set; }

    /// <summary>Extra origins for connect-src (for example a second Application Insights region).</summary>
    public string[] AdditionalConnectSources { get; set; } = [];

    /// <summary>
    /// Serve <see cref="SilentRedirectPath"/> with frame-ancestors 'self' so MSAL can use it as the
    /// redirect URI of silent (iframe) token requests. Off by default: it also requires registering
    /// that redirect URI in Entra ID (see docs/hardening/acciones-azure.md).
    /// </summary>
    public bool SilentRedirectPageEnabled { get; set; }

    public const string SilentRedirectPath = "/redirect.html";
}

/// <summary>
/// Builds the Content-Security-Policy from the endpoints the SPA really uses.
/// </summary>
/// <remarks>
/// - Entra ID (MSAL): token, discovery and silent-renew endpoints are all on https://login.microsoftonline.com
///   (authority in frontend/src/config/authConfig.ts).
/// - Application Insights (browser SDK, bundled from npm, not loaded from the CDN): ingestion and live
///   metrics hosts are read from APPLICATIONINSIGHTS_FRONTEND_CONNECTION_STRING (the same value the SPA is
///   built with). Only the hosts are used; the connection string is never logged.
///   The SDK also fetches its dynamic configuration (scripts/b/ai.config.1.cfg.json) from
///   https://js.monitor.azure.com, so that exact origin is in connect-src (fetch only; script-src stays 'self').
/// - style-src 'unsafe-inline' is required: Fluent UI v9 (Griffel, used by FluentProvider, Spinner and
///   ErrorBoundary via makeStyles) injects &lt;style&gt; elements at runtime, and React renders inline
///   style attributes (App.tsx loading screen). Scripts stay 'self' only.
/// </remarks>
public static class ContentSecurityPolicyBuilder
{
    public const string EntraOrigin = "https://login.microsoftonline.com";

    /// <summary>Origin from which the Application Insights browser SDK fetches its dynamic configuration (CDN config file).</summary>
    public const string AppInsightsConfigOrigin = "https://js.monitor.azure.com";

    public static string Build(IEnumerable<string> connectSources, bool allowSelfFraming)
    {
        var connect = string.Join(' ', new[] { "'self'", EntraOrigin, AppInsightsConfigOrigin }.Concat(connectSources).Distinct(StringComparer.OrdinalIgnoreCase));
        var frameSrc = allowSelfFraming ? $"'self' {EntraOrigin}" : EntraOrigin;

        return string.Join("; ",
            "default-src 'self'",
            "base-uri 'self'",
            "object-src 'none'",
            "frame-ancestors 'none'",
            "form-action 'self'",
            "script-src 'self'",
            "style-src 'self' 'unsafe-inline'",
            "img-src 'self' data: blob:",
            "font-src 'self' data:",
            $"connect-src {connect}",
            $"frame-src {frameSrc}");
    }

    /// <summary>Origins (scheme://host) of IngestionEndpoint and LiveEndpoint in an App Insights connection string.</summary>
    public static IEnumerable<string> AppInsightsOrigins(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            yield break;
        }

        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2
                && (kv[0].Equals("IngestionEndpoint", StringComparison.OrdinalIgnoreCase) || kv[0].Equals("LiveEndpoint", StringComparison.OrdinalIgnoreCase))
                && Uri.TryCreate(kv[1], UriKind.Absolute, out var uri)
                && uri.Scheme == Uri.UriSchemeHttps)
            {
                yield return $"{uri.Scheme}://{uri.Host}";
            }
        }
    }
}

public static class SecurityHeadersExtensions
{
    /// <summary>
    /// Container Apps terminates TLS at the ingress and forwards X-Forwarded-For/Proto. Trusting the
    /// forwarded headers lets the app see the real scheme (needed by HSTS). The container is only
    /// reachable through the ingress, so any upstream proxy is accepted, with a single hop.
    /// </summary>
    public static IServiceCollection AddIngressForwardedHeaders(this IServiceCollection services) =>
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

    /// <summary>Adds nosniff, Referrer-Policy, X-Frame-Options, Permissions-Policy and CSP to every response.</summary>
    public static IApplicationBuilder UseSecurityHeaders(this WebApplication app)
    {
        var settings = app.Configuration.GetSection(SecurityHeadersOptions.SectionName).Get<SecurityHeadersOptions>()
            ?? new SecurityHeadersOptions();

        var connectSources = ContentSecurityPolicyBuilder
            .AppInsightsOrigins(app.Configuration["APPLICATIONINSIGHTS_FRONTEND_CONNECTION_STRING"])
            .Concat(settings.AdditionalConnectSources);

        var policy = string.IsNullOrWhiteSpace(settings.ContentSecurityPolicy)
            ? ContentSecurityPolicyBuilder.Build(connectSources, settings.SilentRedirectPageEnabled)
            : settings.ContentSecurityPolicy;
        var silentRedirectPolicy = policy.Replace("frame-ancestors 'none'", "frame-ancestors 'self'", StringComparison.Ordinal);
        var headerName = settings.ReportOnly ? "Content-Security-Policy-Report-Only" : "Content-Security-Policy";

        return app.Use(async (context, next) =>
        {
            var isSilentRedirectPage = settings.SilentRedirectPageEnabled
                && context.Request.Path.Equals(SecurityHeadersOptions.SilentRedirectPath, StringComparison.OrdinalIgnoreCase);

            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers["X-Content-Type-Options"] = "nosniff";
                headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
                headers["X-Frame-Options"] = isSilentRedirectPage ? "SAMEORIGIN" : "DENY";
                headers[headerName] = isSilentRedirectPage ? silentRedirectPolicy : policy;
                return Task.CompletedTask;
            });

            await next();
        });
    }
}
