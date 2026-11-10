using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using WebApp.Api.Models;
using WebApp.Api.Services;

namespace WebApp.Api.Tests.TestSupport;

/// <summary>
/// Minimal in-memory stand-in for Foundry behind <see cref="IAgentFrameworkService"/>.
/// Streams a fixed script: two text chunks, one annotation, then usage.
/// </summary>
public sealed class FakeFoundry : IAgentFrameworkService
{
    private readonly ConcurrentDictionary<string, string?> _conversations = new();
    private int _sequence;

    public int StreamCalls;
    public int CleanupCalls;
    public int DownloadCalls;
    public string? LastStreamConversationId;
    public McpApprovalResponse? LastApproval;

    public IReadOnlyCollection<string> ConversationIds => _conversations.Keys.ToList();

    /// <summary>Adds a conversation as if it had been created before ownership existed.</summary>
    public void SeedLegacyConversation(string id, string title) => _conversations[id] = title;

    public async IAsyncEnumerable<StreamChunk> StreamMessageAsync(
        string conversationId,
        string message,
        List<string>? imageDataUris = null,
        List<FileAttachment>? fileDataUris = null,
        string? previousResponseId = null,
        McpApprovalResponse? mcpApproval = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref StreamCalls);
        LastStreamConversationId = conversationId;
        LastApproval = mcpApproval;

        yield return StreamChunk.Text("Hola");
        yield return StreamChunk.Text(" mundo");
        yield return StreamChunk.WithAnnotations([new AnnotationInfo { Type = "file_citation", Label = "Caso", FileId = "file_cited" }]);
        await Task.CompletedTask;
    }

    public Task<string> CreateConversationAsync(string? firstMessage = null, CancellationToken cancellationToken = default)
    {
        var id = $"conv_{Interlocked.Increment(ref _sequence):D4}";
        _conversations[id] = firstMessage;
        return Task.FromResult(id);
    }

    public Task<List<ConversationSummary>> ListConversationsAsync(int limit = 20, CancellationToken cancellationToken = default) =>
        Task.FromResult(_conversations
            .OrderByDescending(c => c.Key)
            .Take(limit + 1)
            .Select(c => new ConversationSummary { Id = c.Key, Title = c.Value })
            .ToList());

    public Task<List<ConversationMessageInfo>> GetConversationMessagesAsync(string conversationId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new List<ConversationMessageInfo> { new() { Role = "user", Content = "historial" } });

    public Task DeleteConversationAsync(string conversationId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Conversation deletion is not yet supported by the Azure.AI.Projects SDK.");

    public Task<(BinaryData Content, string FileName)> DownloadFileAsync(string fileId, string? containerId = null, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref DownloadCalls);
        return Task.FromResult((BinaryData.FromString("contenido"), "reporte.csv"));
    }

    public Task<AgentMetadataResponse> GetAgentMetadataAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new AgentMetadataResponse
        {
            Id = "Daily-Enhanced",
            Object = "agent",
            CreatedAt = 1,
            Name = "Daily",
            Model = "gpt-test",
            Instructions = "SYSTEM PROMPT (must not be exposed)",
            Metadata = new Dictionary<string, string> { ["logo"] = "daily.svg", ["internal"] = "x" },
            StarterPrompts = ["Hola"]
        });

    public Task<string> GetAgentInfoAsync(CancellationToken cancellationToken = default) => Task.FromResult("Daily");

    public (int InputTokens, int OutputTokens, int TotalTokens)? GetLastUsage() => (10, 5, 15);

    public Task<UploadedFilesInfo> ListUploadedFilesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new UploadedFilesInfo(2, 2048));

    public Task<UploadedFilesCleanupResult> CleanupUploadedFilesAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref CleanupCalls);
        return Task.FromResult(new UploadedFilesCleanupResult(2, 0));
    }
}
