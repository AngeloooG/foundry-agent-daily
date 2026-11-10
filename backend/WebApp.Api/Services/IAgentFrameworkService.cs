using WebApp.Api.Models;

namespace WebApp.Api.Services;

/// <summary>
/// Minimal seam over the Foundry agent operations used by the API endpoints.
/// </summary>
/// <remarks>
/// Introduced (Lote 1) only so integration tests can host the real API pipeline with a fake
/// Foundry. It mirrors the existing public surface of <see cref="AgentFrameworkService"/> one to one;
/// no behavior changes. Later lotes will narrow it (ownership, removal of admin operations).
/// </remarks>
public interface IAgentFrameworkService
{
    IAsyncEnumerable<StreamChunk> StreamMessageAsync(
        string conversationId,
        string message,
        List<string>? imageDataUris = null,
        List<FileAttachment>? fileDataUris = null,
        string? previousResponseId = null,
        McpApprovalResponse? mcpApproval = null,
        CancellationToken cancellationToken = default);

    Task<string> CreateConversationAsync(string? firstMessage = null, CancellationToken cancellationToken = default);

    Task<List<ConversationSummary>> ListConversationsAsync(int limit = 20, CancellationToken cancellationToken = default);

    Task<List<ConversationMessageInfo>> GetConversationMessagesAsync(string conversationId, CancellationToken cancellationToken = default);

    Task DeleteConversationAsync(string conversationId, CancellationToken cancellationToken = default);

    Task<(BinaryData Content, string FileName)> DownloadFileAsync(string fileId, string? containerId = null, CancellationToken cancellationToken = default);

    Task<AgentMetadataResponse> GetAgentMetadataAsync(CancellationToken cancellationToken = default);

    Task<string> GetAgentInfoAsync(CancellationToken cancellationToken = default);

    (int InputTokens, int OutputTokens, int TotalTokens)? GetLastUsage();

    Task<UploadedFilesInfo> ListUploadedFilesAsync(CancellationToken cancellationToken = default);

    Task<UploadedFilesCleanupResult> CleanupUploadedFilesAsync(CancellationToken cancellationToken = default);
}
