using WebApp.Api.Models;

namespace WebApp.Api.Services;

/// <summary>
/// Seam over the Foundry agent operations used by the API endpoints (substituted by a fake in tests).
/// </summary>
/// <remarks>
/// Ownership is enforced by the endpoints (resource-based authorization) before these methods are
/// called; conversation creation and listing receive the caller's owner key.
/// </remarks>
public interface IAgentFrameworkService
{
    /// <summary>
    /// Streams the agent response. <paramref name="attachments"/> must already be validated
    /// (<see cref="AttachmentProcessor"/>); with <paramref name="mcpApproval"/> the call resumes a
    /// pending MCP tool call instead of sending a user message.
    /// </summary>
    IAsyncEnumerable<StreamChunk> StreamMessageAsync(
        string conversationId,
        string message,
        PreparedAttachments attachments,
        McpApprovalResponse? mcpApproval = null,
        CancellationToken cancellationToken = default);

    /// <summary>Creates a conversation whose metadata records <paramref name="ownerKey"/> as owner.</summary>
    Task<string> CreateConversationAsync(string ownerKey, string? firstMessage = null, CancellationToken cancellationToken = default);

    /// <summary>Conversations owned by <paramref name="ownerKey"/> (up to limit+1 to signal another page).</summary>
    Task<List<ConversationSummary>> ListConversationsAsync(string ownerKey, int limit = 20, CancellationToken cancellationToken = default);

    Task<List<ConversationMessageInfo>> GetConversationMessagesAsync(string conversationId, CancellationToken cancellationToken = default);

    Task DeleteConversationAsync(string conversationId, CancellationToken cancellationToken = default);

    Task<(BinaryData Content, string FileName)> DownloadFileAsync(string fileId, string? containerId = null, CancellationToken cancellationToken = default);

    Task<AgentMetadataResponse> GetAgentMetadataAsync(CancellationToken cancellationToken = default);

    Task<string> GetAgentInfoAsync(CancellationToken cancellationToken = default);

    (int InputTokens, int OutputTokens, int TotalTokens)? GetLastUsage();
}
