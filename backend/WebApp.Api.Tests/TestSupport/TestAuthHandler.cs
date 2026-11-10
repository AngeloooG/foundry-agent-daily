using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WebApp.Api.Tests.TestSupport;

/// <summary>
/// Test-only authentication that builds an Entra-like principal from request headers
/// (no real tokens are created or logged). No user header = anonymous request.
/// </summary>
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string UserHeader = "X-Test-Oid";
    public const string TenantHeader = "X-Test-Tid";
    public const string ScopesHeader = "X-Test-Scp";

    /// <summary>CONSEIN tenant (configured tenant of the app registration).</summary>
    public const string HomeTenantId = "48235e11-59f7-416c-9d73-f5d23b9e3ad2";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserHeader, out var objectId) || string.IsNullOrEmpty(objectId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new("oid", objectId.ToString()),
            new("tid", Request.Headers.TryGetValue(TenantHeader, out var tid) ? tid.ToString() : HomeTenantId),
            new("scp", Request.Headers.TryGetValue(ScopesHeader, out var scopes) ? scopes.ToString() : "Chat.ReadWrite")
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}
