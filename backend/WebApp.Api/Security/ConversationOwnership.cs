using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;

namespace WebApp.Api.Security;

/// <summary>A file the agent cited or generated in a conversation (container id only for code interpreter files).</summary>
public sealed record AuthorizedFile(string FileId, string? ContainerId);

/// <summary>
/// Ownership data kept in Foundry conversation metadata (the source of truth; there is no local store).
/// </summary>
public interface IConversationOwnershipStore
{
    /// <summary>Owner key stored in the conversation, or null when it does not exist or has no owner.</summary>
    Task<string?> GetOwnerAsync(string conversationId, CancellationToken cancellationToken);

    /// <summary>Records files cited/generated in the conversation so their owner can download them.</summary>
    Task AddAuthorizedFilesAsync(string conversationId, IReadOnlyCollection<AuthorizedFile> files, CancellationToken cancellationToken);

    Task<bool> IsFileAuthorizedAsync(string conversationId, string fileId, string? containerId, CancellationToken cancellationToken);
}

/// <summary>Resource-based requirement: the caller must own the conversation (resource = conversation id).</summary>
public sealed class ConversationOwnerRequirement : IAuthorizationRequirement
{
    public static readonly ConversationOwnerRequirement Instance = new();
}

/// <summary>
/// Succeeds only when the conversation exists and its stored owner equals the caller's owner key.
/// Endpoints translate a failure into 404 so the existence of other users' conversations is not disclosed.
/// </summary>
public sealed class ConversationOwnerAuthorizationHandler(
    IUserIdentityAccessor identity,
    IConversationOwnershipStore store,
    IHttpContextAccessor httpContextAccessor,
    ILogger<ConversationOwnerAuthorizationHandler> logger)
    : AuthorizationHandler<ConversationOwnerRequirement, string>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ConversationOwnerRequirement requirement,
        string conversationId)
    {
        var callerKey = identity.GetOwnerKey(context.User);
        if (callerKey is null || !ConversationMetadata.IsValidConversationId(conversationId))
        {
            return;
        }

        var cancellationToken = httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;
        var ownerKey = await store.GetOwnerAsync(conversationId, cancellationToken);
        if (ownerKey is not null && ConversationMetadata.KeysMatch(ownerKey, callerKey))
        {
            context.Succeed(requirement);
            return;
        }

        logger.LogWarning("Conversation access denied: caller is not the owner or the conversation has no owner");
    }
}

/// <summary>
/// Pure helpers for the ownership metadata stored in Foundry conversations.
/// </summary>
/// <remarks>
/// Foundry (OpenAI Conversations) metadata allows at most 16 keys with values of up to 512 characters.
/// Layout: "owner" (64 hex chars), "title" (≤ 53 chars) and authorized files in "files0".."files9",
/// each a comma-separated list of "fileId" or "containerId|fileId". When full, the oldest entries are
/// dropped (their files stop being downloadable). Updating replaces the whole dictionary, so callers
/// must merge with the current metadata.
/// </remarks>
public static partial class ConversationMetadata
{
    public const string OwnerKey = "owner";
    public const string TitleKey = "title";
    public const string FilesKeyPrefix = "files";
    public const int MaxFileKeys = 10;
    public const int MaxValueLength = 512;
    public const int MaxTitleLength = 50;

    public static bool IsValidConversationId(string? conversationId) =>
        !string.IsNullOrEmpty(conversationId) && ConversationIdPattern().IsMatch(conversationId);

    /// <summary>Constant-time comparison of two owner keys.</summary>
    public static bool KeysMatch(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));

    /// <summary>Metadata for a new conversation: owner plus a single-line, truncated title.</summary>
    public static Dictionary<string, string> ForNewConversation(string ownerKey, string? firstMessage)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal) { [OwnerKey] = ownerKey };
        if (!string.IsNullOrWhiteSpace(firstMessage))
        {
            var singleLine = string.Join(' ', firstMessage.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            metadata[TitleKey] = singleLine.Length > MaxTitleLength ? singleLine[..MaxTitleLength] + "..." : singleLine;
        }
        return metadata;
    }

    public static string Encode(AuthorizedFile file) =>
        file.ContainerId is null ? file.FileId : $"{file.ContainerId}|{file.FileId}";

    public static IReadOnlyList<string> ReadFiles(IReadOnlyDictionary<string, string> metadata) =>
        Enumerable.Range(0, MaxFileKeys)
            .Select(i => metadata.GetValueOrDefault(FilesKeyPrefix + i))
            .Where(v => !string.IsNullOrEmpty(v))
            .SelectMany(v => v!.Split(',', StringSplitOptions.RemoveEmptyEntries))
            .ToList();

    public static bool ContainsFile(IReadOnlyDictionary<string, string> metadata, string fileId, string? containerId)
    {
        var wanted = Encode(new AuthorizedFile(fileId, string.IsNullOrEmpty(containerId) ? null : containerId));
        return ReadFiles(metadata).Contains(wanted, StringComparer.Ordinal);
    }

    /// <summary>Returns a copy of <paramref name="metadata"/> with the files merged into files0..files9.</summary>
    public static Dictionary<string, string> WithFiles(IReadOnlyDictionary<string, string> metadata, IEnumerable<AuthorizedFile> files)
    {
        var entries = ReadFiles(metadata).ToList();
        foreach (var encoded in files.Select(Encode).Where(IsStorable))
        {
            if (!entries.Contains(encoded, StringComparer.Ordinal))
            {
                entries.Add(encoded);
            }
        }

        // Pack newest-last; if it does not fit, drop the oldest entries.
        List<string> values;
        while ((values = Pack(entries)).Count > MaxFileKeys)
        {
            entries.RemoveAt(0);
        }

        var result = metadata
            .Where(kvp => !kvp.Key.StartsWith(FilesKeyPrefix, StringComparison.Ordinal))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal);
        for (var i = 0; i < values.Count; i++)
        {
            result[FilesKeyPrefix + i] = values[i];
        }
        return result;
    }

    private static List<string> Pack(IEnumerable<string> entries)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        foreach (var entry in entries)
        {
            if (current.Length > 0 && current.Length + 1 + entry.Length > MaxValueLength)
            {
                values.Add(current.ToString());
                current.Clear();
            }
            if (current.Length > 0) current.Append(',');
            current.Append(entry);
        }
        if (current.Length > 0) values.Add(current.ToString());
        return values;
    }

    // Ids come from Foundry annotations; reject anything that could break the encoding.
    private static bool IsStorable(string encoded) =>
        encoded.Length is > 0 and <= 200 && !encoded.Contains(',') && encoded.Count(c => c == '|') <= 1;

    [GeneratedRegex("^[A-Za-z0-9_-]{1,128}$")]
    private static partial Regex ConversationIdPattern();
}
