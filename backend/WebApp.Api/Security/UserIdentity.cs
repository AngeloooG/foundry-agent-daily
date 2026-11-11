using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Identity.Web;

namespace WebApp.Api.Security;

/// <summary>Resolves the pseudonymous owner key of the signed-in user.</summary>
public interface IUserIdentityAccessor
{
    /// <summary>Owner key of <paramref name="user"/>, or null when the token has no oid/tid.</summary>
    string? GetOwnerKey(ClaimsPrincipal user);
}

/// <summary>Default <see cref="IUserIdentityAccessor"/> based on the token claims.</summary>
public sealed class ClaimsUserIdentityAccessor : IUserIdentityAccessor
{
    public string? GetOwnerKey(ClaimsPrincipal user) => UserIdentity.GetOwnerKey(user);
}

/// <summary>
/// Owner key and tenant helpers.
/// </summary>
/// <remarks>
/// Owner key = SHA-256 of "{tid}:{oid}" in lowercase hex. It pseudonymizes the user but is still
/// personal data (anyone who knows tid and oid can recompute it). tid:oid is never stored in clear
/// in metadata, logs or telemetry. A keyed HMAC (key in Key Vault) is a possible future evolution.
/// </remarks>
public static class UserIdentity
{
    public static string? GetOwnerKey(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        // Microsoft.Identity.Web helpers accept both short ("oid"/"tid") and mapped claim types.
        var objectId = user.GetObjectId();
        var tenantId = user.GetTenantId();
        if (string.IsNullOrWhiteSpace(objectId) || string.IsNullOrWhiteSpace(tenantId))
        {
            return null;
        }

        return ComputeOwnerKey(tenantId, objectId);
    }

    /// <summary>First 16 hex chars of the owner key, for log correlation (never the raw oid).</summary>
    public static string? GetLogKey(ClaimsPrincipal? user) => GetOwnerKey(user)?[..16];

    public static string ComputeOwnerKey(string tenantId, string objectId) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{tenantId}:{objectId}")));

    /// <summary>True when the token's tid equals the configured (CONSEIN) tenant. Fails closed.</summary>
    public static bool IsAllowedTenant(ClaimsPrincipal user, string? allowedTenantId) =>
        !string.IsNullOrWhiteSpace(allowedTenantId)
        && string.Equals(user.GetTenantId(), allowedTenantId, StringComparison.OrdinalIgnoreCase);
}
