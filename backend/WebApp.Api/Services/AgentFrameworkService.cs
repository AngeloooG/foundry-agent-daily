using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.AI.Extensions.OpenAI;
using Azure.Core;
using Azure.Identity;
using OpenAI.Files;
using OpenAI.Responses;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web;
using System.ClientModel;
using System.Runtime.CompilerServices;
using System.Text;
using WebApp.Api.Infrastructure;
using WebApp.Api.Models;
using WebApp.Api.Security;

namespace WebApp.Api.Services;

#pragma warning disable OPENAI001

/// <summary>
/// Foundry Agent Service using v2 Agents API.
/// </summary>
/// <remarks>
/// Uses AIProjectClient directly (Azure.AI.Projects GA): AgentAdministrationClient for agent
/// metadata and ProjectResponsesClient for streaming (required for annotations, MCP approvals).
/// See .github/skills/researching-azure-ai-sdk/SKILL.md for SDK patterns.
/// </remarks>
public class AgentFrameworkService : IAgentFrameworkService, IConversationOwnershipStore, IDisposable
{
    /// <summary>Upper bound of conversations scanned when listing (ownership is filtered app-side).</summary>
    internal const int MaxConversationsScanned = 500;

    private readonly string _agentEndpoint;
    private readonly string _agentId;
    /// <summary>
    /// Optional concrete agent version id (e.g. "3") from <c>AI_AGENT_VERSION</c>.
    /// When set, the agent is pinned to that immutable version for both metadata
    /// (<see cref="GetAgentAsync"/>) and streaming (<c>AgentReference</c> passed to
    /// <c>ProjectResponsesClient</c>). When null, the newest published version is
    /// resolved on startup and used consistently. Foundry retains all published
    /// versions, so pinning is useful for reproducibility across deployments.
    /// </summary>
    private readonly string? _configuredAgentVersion;
    private readonly ILogger<AgentFrameworkService> _logger;
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly string? _backendClientId;
    private readonly string? _tenantId;
    private readonly string? _managedIdentityClientId;
    private readonly bool _useObo;
    private readonly TokenCredential _fallbackCredential;

    // Agent metadata cache (static - shared across requests)
    private static ProjectsAgentVersion? s_cachedAgentVersion;
    private static AgentMetadataResponse? s_cachedMetadata;
    private static readonly SemaphoreSlim s_agentLock = new(1, 1);
    // MI assertion cache (static - user-independent, safe to share across requests)
    private static ManagedIdentityClientAssertion? s_miAssertion;

    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Prefix applied to image files this web app uploads to the Foundry Files API, so they can be
    /// told apart from other files in the shared Foundry project (retention is an operational task;
    /// there is no public cleanup endpoint).
    /// </summary>
    public const string WebAppUploadFilenamePrefix = "webapp-upload-";

    // Per-request project client
    private AIProjectClient? _projectClient;
    private bool _disposed = false;
    private ResponseTokenUsage? _lastUsage;

    public AgentFrameworkService(
        IConfiguration configuration,
        ILogger<AgentFrameworkService> logger,
        IHttpClientFactory httpClientFactory,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _httpContextAccessor = httpContextAccessor;

        _agentEndpoint = configuration["AI_AGENT_ENDPOINT"]
            ?? throw new InvalidOperationException("AI_AGENT_ENDPOINT is not configured");

        _agentId = configuration["AI_AGENT_ID"]
            ?? throw new InvalidOperationException("AI_AGENT_ID is not configured");

        _configuredAgentVersion = string.IsNullOrWhiteSpace(configuration["AI_AGENT_VERSION"])
            ? null
            : configuration["AI_AGENT_VERSION"];

        _logger.LogDebug(
            "Initializing AgentFrameworkService: endpoint={Endpoint}, agentId={AgentId}, version={Version}", 
            _agentEndpoint, 
            _agentId,
            _configuredAgentVersion ?? "<latest>");

        _backendClientId = configuration["ENTRA_BACKEND_CLIENT_ID"];
        _tenantId = configuration["ENTRA_TENANT_ID"] ?? configuration["AzureAd:TenantId"];
        // User-assigned MI client ID — used for MI-only mode and as FIC assertion in OBO mode
        _managedIdentityClientId = configuration["MANAGED_IDENTITY_CLIENT_ID"]
            ?? configuration["OBO_MANAGED_IDENTITY_CLIENT_ID"]; // backward compat

        var environment = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production";

        // Determine if OBO is available
        _useObo = !string.IsNullOrEmpty(_backendClientId)
                  && !string.IsNullOrEmpty(_tenantId)
                  && environment != "Development";

        // Create credential for non-OBO operations (agent metadata cache, MI-only mode)
        if (environment == "Development")
        {
            _logger.LogInformation("Development: Using ChainedTokenCredential (AzureCli -> AzureDeveloperCli)");
            _fallbackCredential = new ChainedTokenCredential(
                new AzureCliCredential(),
                new AzureDeveloperCliCredential()
            );
        }
        else if (!string.IsNullOrEmpty(_managedIdentityClientId))
        {
            _logger.LogInformation("Production: Using user-assigned ManagedIdentityCredential: {MiClientId}", _managedIdentityClientId);
            _fallbackCredential = new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(_managedIdentityClientId));
        }
        else
        {
            _logger.LogInformation("Production: Using ManagedIdentityCredential (system-assigned)");
            _fallbackCredential = new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned);
        }

        if (_useObo)
        {
            if (string.IsNullOrEmpty(_managedIdentityClientId))
            {
                throw new InvalidOperationException(
                    "OBO mode requires MANAGED_IDENTITY_CLIENT_ID to be set for the FIC assertion. " +
                    "This is the user-assigned managed identity that acts as the federated credential.");
            }
            _logger.LogInformation("OBO mode enabled: backendClientId={BackendClientId}. All API calls use user-delegated identity.", _backendClientId);

            // Initialize MI assertion eagerly — avoids thread-safety issues with lazy init
            // in CreateOboCredential(). Safe here because the constructor runs once per scoped instance.
            s_miAssertion ??= new ManagedIdentityClientAssertion(managedIdentityClientId: _managedIdentityClientId);

            // No cached project client in OBO mode — created per-request with user's token
        }
        else
        {
            _logger.LogInformation("MI mode: using managed identity for all API calls");
            _projectClient = new AIProjectClient(new Uri(_agentEndpoint), _fallbackCredential);
        }

        _logger.LogInformation("AIProjectClient initialized successfully");
    }

    /// <summary>
    /// Get AIProjectClient — OBO mode creates per-request with user's identity, MI mode uses cached client.
    /// </summary>
    private AIProjectClient GetProjectClient()
    {
        // MI mode: return cached client
        if (!_useObo)
        {
            _projectClient ??= new AIProjectClient(new Uri(_agentEndpoint), _fallbackCredential);
            return _projectClient;
        }

        // OBO: create per-request client with user's token (cached for request lifetime)
        if (_projectClient is null)
        {
            var userToken = ExtractBearerToken();
            if (string.IsNullOrEmpty(userToken))
            {
                throw new InvalidOperationException(
                    "OBO mode requires a bearer token but none was found in the request. " +
                    "Ensure the frontend is sending an Authorization header with a valid token.");
            }

            var oboCredential = CreateOboCredential(userToken);
            _logger.LogDebug("Created OBO credential for request");
            _projectClient = new AIProjectClient(new Uri(_agentEndpoint), oboCredential);
        }

        return _projectClient;
    }

    /// <summary>
    /// Create OBO credential using the user's JWT and managed identity FIC assertion.
    /// </summary>
    private OnBehalfOfCredential CreateOboCredential(string userToken)
    {
        // s_miAssertion is initialized eagerly in the constructor (OBO branch)
        Func<CancellationToken, Task<string>> assertionCallback =
            async (ct) => await s_miAssertion!.GetSignedAssertionAsync(
                new AssertionRequestOptions { CancellationToken = ct });

        return new OnBehalfOfCredential(
            _tenantId!,
            _backendClientId!,
            assertionCallback,
            userToken,
            new OnBehalfOfCredentialOptions());
    }

    /// <summary>
    /// Extract bearer token from the current HTTP request.
    /// </summary>
    private string? ExtractBearerToken()
    {
        var authHeader = _httpContextAccessor?.HttpContext?.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        return authHeader["Bearer ".Length..].Trim();
    }

    /// <summary>
    /// Load the agent version metadata via AgentAdministrationClient (v2 Agents API).
    /// When <see cref="_configuredAgentVersion"/> is set, fetches that specific version by id.
    /// When unset, lists versions in descending order and picks the first (= newest).
    /// </summary>
    private async Task<ProjectsAgentVersion> GetAgentAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (s_cachedAgentVersion != null)
            return s_cachedAgentVersion;

        await s_agentLock.WaitAsync(cancellationToken);
        try
        {
            if (s_cachedAgentVersion != null)
                return s_cachedAgentVersion;

            // Use the same credential path as all other operations (MI or OBO)
            var client = GetProjectClient();

            ProjectsAgentVersion? loaded;
            if (!string.IsNullOrWhiteSpace(_configuredAgentVersion))
            {
                _logger.LogInformation("Loading agent: {AgentId} version={Version}", _agentId, _configuredAgentVersion);
                var response = await client.AgentAdministrationClient.GetAgentVersionAsync(
                    _agentId,
                    _configuredAgentVersion!,
                    cancellationToken);
                loaded = response.Value;
            }
            else
            {
                _logger.LogInformation("Loading agent: {AgentId} version=<latest>", _agentId);
                loaded = null;
                await foreach (var v in client.AgentAdministrationClient.GetAgentVersionsAsync(
                    agentName: _agentId,
                    limit: 1,
                    order: AgentListOrder.Descending,
                    after: null,
                    before: null,
                    cancellationToken: cancellationToken))
                {
                    loaded = v;
                    break;
                }

                if (loaded is null)
                {
                    throw new InvalidOperationException(
                        $"Agent '{_agentId}' has no versions. Create at least one version in AI Foundry.");
                }
            }

            s_cachedAgentVersion = loaded;

            var definition = s_cachedAgentVersion.Definition as DeclarativeAgentDefinition;

            _logger.LogInformation(
                "Loaded agent: name={AgentName}, model={Model}, version={Version} (pinned={Pinned})",
                s_cachedAgentVersion.Name ?? _agentId,
                definition?.Model ?? "unknown",
                s_cachedAgentVersion.Version ?? "<unknown>",
                !string.IsNullOrWhiteSpace(_configuredAgentVersion));

            // Log StructuredInputs at debug level for troubleshooting
            if (definition?.StructuredInputs != null && definition.StructuredInputs.Count > 0)
            {
                _logger.LogDebug("Agent has {Count} StructuredInputs: {Keys}",
                    definition.StructuredInputs.Count,
                    string.Join(", ", definition.StructuredInputs.Keys));
            }

            return s_cachedAgentVersion;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load agent: {AgentId}", _agentId);
            throw;
        }
        finally
        {
            s_agentLock.Release();
        }
    }

    /// <summary>
    /// Streams agent response for a message using ProjectResponsesClient (Responses API).
    /// Returns StreamChunk objects containing text deltas, annotations, or MCP approval requests.
    /// </summary>
    /// <remarks>
    /// Uses direct ProjectResponsesClient instead of IChatClient because we need access to:
    /// - McpToolCallApprovalRequestItem for MCP approval flows
    /// - FileSearchCallResponseItem for file search quotes  
    /// - MessageResponseItem.OutputTextAnnotations for citations
    /// The IChatClient abstraction doesn't expose these specialized response types.
    /// </remarks>
    public async IAsyncEnumerable<StreamChunk> StreamMessageAsync(
        string conversationId,
        string message,
        PreparedAttachments attachments,
        McpApprovalResponse? mcpApproval = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _logger.LogInformation(
            "Streaming message to conversation: {ConversationId}, Images: {ImageCount}, Texts: {TextCount}, Pdfs: {PdfCount}, HasApproval: {HasApproval}",
            conversationId,
            attachments.Images.Count,
            attachments.InlineTexts.Count,
            attachments.Pdfs.Count,
            mcpApproval != null);

        CreateResponseOptions options = new() { StreamingEnabled = true };

        // Resolve the concrete agent version up front so streaming and metadata use the same version.
        var resolvedAgent = await GetAgentAsync(cancellationToken);
        var resolvedVersion = _configuredAgentVersion ?? resolvedAgent.Version;

        // Always bind to conversation — the conversation maintains MCP approval state
        ProjectResponsesClient responsesClient
            = GetProjectClient().ProjectOpenAIClient.GetProjectResponsesClientForAgent(
                new AgentReference(_agentId, resolvedVersion),
                conversationId);

        // If continuing from MCP approval, add approval response items
        // Don't set PreviousResponseId — the API rejects it with conversation binding,
        // and the conversation already tracks the pending MCP state
        if (mcpApproval != null)
        {
            options.InputItems.Add(ResponseItem.CreateMcpApprovalResponseItem(
                mcpApproval.ApprovalRequestId,
                mcpApproval.Approved));
            
            _logger.LogInformation(
                "Resuming with MCP approval: RequestId={RequestId}, Approved={Approved}",
                mcpApproval.ApprovalRequestId,
                mcpApproval.Approved);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                _logger.LogWarning("Attempted to stream empty message to conversation {ConversationId}", conversationId);
                throw new ArgumentException("Message cannot be null or whitespace", nameof(message));
            }

            // Build user message with optional (already validated) images and files
            ResponseItem userMessage = await BuildUserMessageAsync(message, attachments, cancellationToken);
            options.InputItems.Add(userMessage);
        }

        // Dictionary to collect file search results for quote extraction
        var fileSearchQuotes = new Dictionary<string, string>();
        // Track the current response ID for MCP approval resume flow
        string? currentResponseId = null;

        await foreach (StreamingResponseUpdate update
            in responsesClient.CreateResponseStreamingAsync(
                options: options,
                cancellationToken: cancellationToken))
        {
            // Capture response ID from created event (needed for MCP approval resume)
            if (update is StreamingResponseCreatedUpdate createdUpdate)
            {
                currentResponseId = createdUpdate.Response.Id;
                _logger.LogDebug("Response created: {ResponseId}", currentResponseId);
                continue;
            }

            if (update is StreamingResponseOutputTextDeltaUpdate deltaUpdate)
            {
                yield return StreamChunk.Text(deltaUpdate.Delta);
            }
            else if (update is StreamingResponseOutputItemDoneUpdate itemDoneUpdate)
            {
                // Check for MCP tool approval request
                if (itemDoneUpdate.Item is McpToolCallApprovalRequestItem mcpApprovalItem)
                {
                    _logger.LogInformation(
                        "MCP tool approval requested: Id={Id}, Tool={Tool}, Server={Server}",
                        mcpApprovalItem.Id,
                        mcpApprovalItem.ToolName,
                        mcpApprovalItem.ServerLabel);
                    
                    // Parse tool arguments from BinaryData to string (JSON)
                    string? argumentsJson = mcpApprovalItem.ToolArguments?.ToString();
                    
                    yield return StreamChunk.McpApproval(new McpApprovalRequest
                    {
                        Id = mcpApprovalItem.Id,
                        ToolName = mcpApprovalItem.ToolName ?? "Unknown tool",
                        ServerLabel = mcpApprovalItem.ServerLabel ?? "MCP Server",
                        Arguments = argumentsJson,
                        PreviousResponseId = currentResponseId
                    });
                    continue;
                }
                
                // Capture file search results for quote extraction
                if (itemDoneUpdate.Item is FileSearchCallResponseItem fileSearchItem)
                {
                    foreach (var result in fileSearchItem.Results)
                    {
                        if (!string.IsNullOrEmpty(result.FileId) && !string.IsNullOrEmpty(result.Text))
                        {
                            fileSearchQuotes[result.FileId] = result.Text;
                            _logger.LogDebug(
                                "Captured file search quote for FileId={FileId}, QuoteLength={Length}", 
                                result.FileId, 
                                result.Text.Length);
                        }
                    }
                    continue;
                }
                
                // Extract annotations/citations from completed output items
                var annotations = ExtractAnnotations(itemDoneUpdate.Item, fileSearchQuotes);
                if (annotations.Count > 0)
                {
                    _logger.LogInformation("Extracted {Count} annotations from response", annotations.Count);
                    yield return StreamChunk.WithAnnotations(annotations);
                }
            }
            else if (update is StreamingResponseOutputItemAddedUpdate itemAddedUpdate)
            {
                // Detect tool-use steps and signal the frontend for progress indicators
                string? toolName = itemAddedUpdate.Item switch
                {
                    FileSearchCallResponseItem => "file_search",
                    CodeInterpreterCallResponseItem => "code_interpreter",
                    _ when itemAddedUpdate.Item?.GetType().Name.Contains("ToolCall") == true => "function_call",
                    _ => null
                };

                if (toolName != null)
                {
                    _logger.LogDebug("Tool use detected: {ToolName}", toolName);
                    yield return StreamChunk.ToolUse(toolName);
                }
            }
            else if (update is StreamingResponseCompletedUpdate completedUpdate)
            {
                _lastUsage = completedUpdate.Response.Usage;
            }
            else if (update is StreamingResponseErrorUpdate errorUpdate)
            {
                _logger.LogError("Stream error: {Error}", errorUpdate.Message);
                throw new InvalidOperationException($"Stream error: {errorUpdate.Message}");
            }
            else
            {
                _logger.LogDebug("Unhandled stream update type: {Type}", update.GetType().Name);
            }
        }

        _logger.LogInformation("Completed streaming for conversation: {ConversationId}", conversationId);
    }

    /// <summary>
    /// Builds the user message from attachments already validated by <see cref="AttachmentProcessor"/>
    /// (validation happens in the endpoint, before any conversation is created or file uploaded).
    /// Images are uploaded to the Foundry Files API (purpose: assistants) and referenced by id; if an
    /// upload fails, images already uploaded for this message are deleted (no orphans).
    /// </summary>
    private async Task<ResponseItem> BuildUserMessageAsync(
        string message,
        PreparedAttachments attachments,
        CancellationToken cancellationToken)
    {
        if (attachments.IsEmpty)
        {
            return ResponseItem.CreateUserMessageItem(message);
        }

        var contentParts = new List<ResponseContentPart> { ResponseContentPart.CreateInputTextPart(message) };
        contentParts.AddRange(attachments.InlineTexts.Select(ResponseContentPart.CreateInputTextPart));

        // Foundry's Files proxy only accepts purpose=assistants for images used with CreateInputImagePart.
        var fileClient = GetProjectClient().ProjectOpenAIClient.GetOpenAIFileClient();
        var uploadedIds = new List<string>();
        try
        {
            foreach (var image in attachments.Images)
            {
                var extension = image.MediaType switch
                {
                    "image/png" => ".png",
                    "image/jpeg" or "image/jpg" => ".jpg",
                    "image/gif" => ".gif",
                    "image/webp" => ".webp",
                    _ => ".bin",
                };
                var imageFileName = $"{WebAppUploadFilenamePrefix}{Guid.NewGuid():N}{extension}";
                using var imageStream = new MemoryStream(image.Bytes, writable: false);
                var uploaded = await fileClient.UploadFileAsync(imageStream, imageFileName, FileUploadPurpose.Assistants, cancellationToken);
                uploadedIds.Add(uploaded.Value.Id);
                contentParts.Add(ResponseContentPart.CreateInputImagePart(uploaded.Value.Id));
            }
        }
        catch
        {
            foreach (var id in uploadedIds)
            {
                try
                {
                    await fileClient.DeleteFileAsync(id, CancellationToken.None);
                }
                catch (Exception deleteEx)
                {
                    _logger.LogWarning(deleteEx, "Could not delete orphaned upload {FileId}", id);
                }
            }
            throw;
        }

        contentParts.AddRange(attachments.Pdfs.Select(pdf =>
            ResponseContentPart.CreateInputFilePart(BinaryData.FromBytes(pdf.Bytes), pdf.MediaType, pdf.FileName)));

        return ResponseItem.CreateUserMessageItem(contentParts);
    }

    /// <summary>
    /// Extracts annotation information from a completed response item.
    /// </summary>
    private List<AnnotationInfo> ExtractAnnotations(
        ResponseItem? item, 
        Dictionary<string, string>? fileSearchQuotes = null)
    {
        var annotations = new List<AnnotationInfo>();
        
        if (item is not MessageResponseItem messageItem)
            return annotations;

        foreach (var content in messageItem.Content)
        {
            if (content.OutputTextAnnotations == null) continue;
            
            foreach (var annotation in content.OutputTextAnnotations)
            {
                var annotationInfo = annotation switch
                {
                    UriCitationMessageAnnotation uriAnnotation => new AnnotationInfo
                    {
                        Type = "uri_citation",
                        Label = uriAnnotation.Title ?? "Source",
                        Url = uriAnnotation.Uri?.ToString(),
                        StartIndex = uriAnnotation.StartIndex,
                        EndIndex = uriAnnotation.EndIndex
                    },
                    
                    FileCitationMessageAnnotation fileCitation => new AnnotationInfo
                    {
                        Type = "file_citation",
                        Label = fileCitation.Filename ?? fileCitation.FileId ?? "File",
                        FileId = fileCitation.FileId,
                        StartIndex = fileCitation.Index,
                        EndIndex = fileCitation.Index,
                        Quote = fileSearchQuotes?.TryGetValue(fileCitation.FileId ?? string.Empty, out var quote) == true 
                            ? quote : null
                    },
                    
                    FilePathMessageAnnotation filePath => new AnnotationInfo
                    {
                        Type = "file_path",
                        Label = filePath.FileId?.Split('/').LastOrDefault() ?? "Generated File",
                        FileId = filePath.FileId,
                        StartIndex = filePath.Index,
                        EndIndex = filePath.Index
                    },
                    
                    ContainerFileCitationMessageAnnotation containerCitation => new AnnotationInfo
                    {
                        Type = "container_file_citation",
                        Label = containerCitation.Filename ?? "Container File",
                        FileId = containerCitation.FileId,
                        ContainerId = containerCitation.ContainerId,
                        StartIndex = containerCitation.StartIndex,
                        EndIndex = containerCitation.EndIndex,
                        Quote = fileSearchQuotes?.TryGetValue(containerCitation.FileId ?? string.Empty, out var containerQuote) == true 
                            ? containerQuote : null
                    },
                    
                    _ => null
                };
                
                if (annotationInfo != null)
                    annotations.Add(annotationInfo);
            }
        }

        return annotations;
    }

    /// <summary>
    /// Create a new conversation owned by <paramref name="ownerKey"/> (stored in Foundry metadata).
    /// Foundry failures propagate: there is no local fallback (the previous in-process store never
    /// persisted across requests and returned ids that Foundry did not know).
    /// </summary>
    public async Task<string> CreateConversationAsync(string ownerKey, string? firstMessage = null, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var conversationOptions = new ProjectConversationCreationOptions();
        foreach (var (key, value) in ConversationMetadata.ForNewConversation(ownerKey, firstMessage))
        {
            conversationOptions.Metadata[key] = value;
        }

        ProjectConversation conversation = await Conversations.CreateProjectConversationAsync(conversationOptions, cancellationToken);
        _logger.LogInformation("Created conversation: {ConversationId}", conversation.Id);
        return conversation.Id;
    }

    /// <summary>
    /// List the caller's conversations for the current agent version. Ownership is filtered here
    /// (Foundry cannot filter by metadata); at most <see cref="MaxConversationsScanned"/> are scanned.
    /// Returns up to limit+1 items so the caller can detect another page.
    /// </summary>
    public async Task<List<ConversationSummary>> ListConversationsAsync(string ownerKey, int limit = 20, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var resolvedAgent = await GetAgentAsync(cancellationToken);
        var resolvedVersion = _configuredAgentVersion ?? resolvedAgent.Version;

        var conversations = new List<ConversationSummary>();
        var scanned = 0;
        await foreach (var conv in Conversations.GetProjectConversationsAsync(
            new AgentReference(_agentId, resolvedVersion), cancellationToken: cancellationToken))
        {
            if (++scanned > MaxConversationsScanned)
                break;

            if (conv.Metadata is null
                || !conv.Metadata.TryGetValue(ConversationMetadata.OwnerKey, out var owner)
                || !ConversationMetadata.KeysMatch(owner, ownerKey))
                continue;

            conversations.Add(new ConversationSummary
            {
                Id = conv.Id,
                Title = conv.Metadata.TryGetValue(ConversationMetadata.TitleKey, out var title) ? title : null,
                CreatedAt = conv.CreatedAt.ToUnixTimeSeconds()
            });

            if (conversations.Count > limit)
                break;
        }

        return conversations;
    }

    private ProjectConversationsClient Conversations =>
        GetProjectClient().ProjectOpenAIClient.GetProjectConversationsClient();

    // ===== IConversationOwnershipStore (Foundry conversation metadata) =====

    public async Task<string?> GetOwnerAsync(string conversationId, CancellationToken cancellationToken)
    {
        var metadata = await GetMetadataAsync(conversationId, cancellationToken);
        return metadata is not null && metadata.TryGetValue(ConversationMetadata.OwnerKey, out var owner) ? owner : null;
    }

    public async Task AddAuthorizedFilesAsync(string conversationId, IReadOnlyCollection<AuthorizedFile> files, CancellationToken cancellationToken)
    {
        if (files.Count == 0)
            return;

        var current = await GetMetadataAsync(conversationId, cancellationToken)
            ?? throw new InvalidOperationException("Conversation not found while recording authorized files.");

        // Update replaces the whole metadata dictionary: merge to keep owner and title.
        var options = new ProjectConversationUpdateOptions();
        foreach (var (key, value) in ConversationMetadata.WithFiles(current, files))
        {
            options.Metadata[key] = value;
        }

        await Conversations.UpdateProjectConversationAsync(conversationId, options, cancellationToken);
    }

    public async Task<bool> IsFileAuthorizedAsync(string conversationId, string fileId, string? containerId, CancellationToken cancellationToken)
    {
        var metadata = await GetMetadataAsync(conversationId, cancellationToken);
        return metadata is not null && ConversationMetadata.ContainsFile(metadata, fileId, containerId);
    }

    /// <summary>Conversation metadata, or null when Foundry reports the conversation does not exist.</summary>
    private async Task<IReadOnlyDictionary<string, string>?> GetMetadataAsync(string conversationId, CancellationToken cancellationToken)
    {
        try
        {
            ProjectConversation conversation = await Conversations.GetProjectConversationAsync(conversationId, cancellationToken);
            return conversation.Metadata is null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string>(conversation.Metadata);
        }
        catch (ClientResultException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    /// <summary>
    /// Get messages for a specific conversation.
    /// </summary>
    public async Task<List<ConversationMessageInfo>> GetConversationMessagesAsync(
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var messages = new List<ConversationMessageInfo>();

        // Filter to message items only
        await foreach (var item in Conversations.GetProjectConversationItemsAsync(
            conversationId, itemKind: AgentResponseItemKind.Message, cancellationToken: cancellationToken))
        {
            if (item.AsResponseResultItem() is MessageResponseItem messageItem)
            {
                messages.Add(new ConversationMessageInfo
                {
                    Role = messageItem.Role.ToString().ToLowerInvariant(),
                    Content = string.Join("", messageItem.Content.Where(c => c.Text != null).Select(c => c.Text))
                });
            }
        }

        // Items are returned newest first; the UI expects chronological order.
        messages.Reverse();
        return messages;
    }

    /// <summary>
    /// Delete a conversation.
    /// </summary>
    /// <remarks>
    /// TODO: The Azure.AI.Projects SDK does not expose a delete conversation API.
    /// This method is a stub that will need to be updated when the SDK adds delete support.
    /// </remarks>
    public Task DeleteConversationAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _logger.LogWarning(
            "DeleteConversationAsync is not yet supported by the SDK. ConversationId: {ConversationId}",
            conversationId);

        // TODO: Replace with actual SDK call when available.
        // The ProjectConversationsClient currently only supports Create, Get, List, and Update.
        throw new NotSupportedException(
            "Conversation deletion is not yet supported by the Azure.AI.Projects SDK.");
    }

    /// <summary>
    /// Download a file generated by code interpreter or other tools, up to <paramref name="maxBytes"/>.
    /// Container files (with containerId) use the REST API: GET /openai/v1/containers/{containerId}/files/{fileId}/content.
    /// Standard files use the OpenAI FileClient.
    /// </summary>
    /// <exception cref="FileTooLargeException">The file exceeds <paramref name="maxBytes"/>. When the size is known
    /// beforehand (file metadata or Content-Length) the content is never downloaded.</exception>
    public async Task<(BinaryData Content, string FileName)> DownloadFileAsync(
        string fileId,
        string? containerId,
        long maxBytes,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            if (!string.IsNullOrEmpty(containerId))
            {
                return await DownloadContainerFileAsync(fileId, containerId, maxBytes, cancellationToken);
            }

            _logger.LogInformation("Downloading standard file: {FileId}", fileId);
            var fileClient = GetProjectClient().ProjectOpenAIClient.GetOpenAIFileClient();

            // Metadata first: a file already known to be too large is never downloaded.
            var fileInfo = await fileClient.GetFileAsync(fileId, cancellationToken);
            DownloadLimits.EnsureWithinLimit(fileInfo.Value?.SizeInBytesLong, maxBytes);

            var fileContent = await fileClient.DownloadFileAsync(fileId, cancellationToken);
            // Defensive: the metadata size may be missing.
            DownloadLimits.EnsureWithinLimit(fileContent.Value.ToMemory().Length, maxBytes);

            var fileName = fileInfo.Value?.Filename ?? $"{fileId}.bin";
            _logger.LogInformation("Downloaded file: {FileId}, Name: {FileName}, Size: {Size} bytes",
                fileId, fileName, fileContent.Value.ToMemory().Length);
            return (fileContent.Value, fileName);
        }
        catch (FileTooLargeException)
        {
            _logger.LogWarning("File {FileId} exceeds the maximum download size ({MaxBytes} bytes)", fileId, maxBytes);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download file {FileId}. Error: {Error}", fileId, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Download a container file via REST API.
    /// Endpoint: GET {projectEndpoint}/openai/v1/containers/{containerId}/files/{fileId}/content
    /// </summary>
    private async Task<(BinaryData Content, string FileName)> DownloadContainerFileAsync(
        string fileId,
        string containerId,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Downloading container file: {FileId} from container: {ContainerId}", fileId, containerId);

        // Reuse the same credential as the project client (MI or OBO)
        TokenCredential credential;
        if (_useObo)
        {
            var userToken = ExtractBearerToken();
            credential = CreateOboCredential(userToken ?? throw new InvalidOperationException("OBO requires bearer token"));
        }
        else
        {
            credential = _fallbackCredential;
        }

        var tokenRequestContext = new TokenRequestContext(["https://ai.azure.com/.default"]);
        var accessToken = await credential.GetTokenAsync(tokenRequestContext, cancellationToken);

        var requestUrl = $"{_agentEndpoint.TrimEnd('/')}/openai/v1/containers/{Uri.EscapeDataString(containerId)}/files/{Uri.EscapeDataString(fileId)}/content";
        using var httpClient = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken.Token);

        // Headers first: the body is only read when it fits in the limit.
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        DownloadLimits.EnsureWithinLimit(response.Content.Headers.ContentLength, maxBytes);

        byte[] bytes;
        await using (var body = await response.Content.ReadAsStreamAsync(cancellationToken))
        {
            // Without Content-Length, stop reading one byte past the limit.
            bytes = await DownloadLimits.ReadBoundedAsync(body, maxBytes, cancellationToken);
        }

        // Try to extract filename from Content-Disposition header, fall back to fileId
        var fileName = $"{fileId}.bin";
        if (response.Content.Headers.ContentDisposition?.FileName is { } headerFileName)
        {
            fileName = headerFileName.Trim('"');
        }

        _logger.LogInformation("Downloaded container file: {FileId}, Name: {FileName}, Size: {Size} bytes",
            fileId, fileName, bytes.Length);
        return (BinaryData.FromBytes(bytes), fileName);
    }

    /// <summary>
    /// Get the agent metadata (name, description, etc.) for display in UI.
    /// Reads directly from the cached ProjectsAgentVersion.
    /// </summary>
    public async Task<AgentMetadataResponse> GetAgentMetadataAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var agentVersion = await GetAgentAsync(cancellationToken);

        if (s_cachedMetadata != null)
            return s_cachedMetadata;

        var definition = agentVersion.Definition as DeclarativeAgentDefinition;
        var metadata = agentVersion.Metadata?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        // Log metadata keys at debug level for troubleshooting
        if (metadata != null && metadata.Count > 0)
        {
            _logger.LogDebug("Agent metadata keys: {Keys}", string.Join(", ", metadata.Keys));
        }

        // Parse starter prompts from metadata
        List<string>? starterPrompts = ParseStarterPrompts(metadata);

        s_cachedMetadata = new AgentMetadataResponse
        {
            Id = _agentId,
            Object = "agent",
            CreatedAt = agentVersion.CreatedAt.ToUnixTimeSeconds(),
            Name = agentVersion.Name ?? "AI Assistant",
            Description = agentVersion.Description,
            Model = definition?.Model ?? string.Empty,
            // Never the instructions (system prompt); only allowlisted metadata keys.
            Metadata = FilterPublicMetadata(metadata),
            StarterPrompts = starterPrompts
        };

        return s_cachedMetadata;
    }

    /// <summary>Agent metadata keys that are safe to return to every signed-in user.</summary>
    internal static readonly IReadOnlySet<string> PublicMetadataKeys = new HashSet<string>(StringComparer.Ordinal) { "logo" };

    internal static Dictionary<string, string>? FilterPublicMetadata(IReadOnlyDictionary<string, string>? metadata)
    {
        var filtered = metadata?
            .Where(kvp => PublicMetadataKeys.Contains(kvp.Key))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        return filtered is { Count: > 0 } ? filtered : null;
    }

    /// <summary>
    /// Parse starter prompts from agent metadata.
    /// Microsoft Foundry stores starter prompts as newline-separated text in the "starterPrompts" metadata key.
    /// Example: "How's the weather?\nIs your fridge running?\nTell me a joke"
    /// </summary>
    private List<string>? ParseStarterPrompts(Dictionary<string, string>? metadata)
    {
        if (metadata == null)
            return null;

        // Microsoft Foundry uses camelCase "starterPrompts" key with newline-separated values
        if (!metadata.TryGetValue("starterPrompts", out var starterPromptsValue))
            return null;

        if (string.IsNullOrWhiteSpace(starterPromptsValue))
            return null;

        // Split by newlines and filter out empty entries
        var prompts = starterPromptsValue
            .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrEmpty(p))
            .ToList();

        if (prompts.Count > 0)
        {
            _logger.LogDebug("Parsed {Count} starter prompts from agent metadata", prompts.Count);
            return prompts;
        }

        return null;
    }

    /// <summary>
    /// Get basic agent info string (for debugging).
    /// </summary>
    public async Task<string> GetAgentInfoAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var agentVersion = await GetAgentAsync(cancellationToken);
        return agentVersion.Name ?? _agentId;
    }

    /// <summary>
    /// Get token usage from the last streaming response.
    /// </summary>
    public (int InputTokens, int OutputTokens, int TotalTokens)? GetLastUsage() =>
        _lastUsage is null ? null : (_lastUsage.InputTokenCount, _lastUsage.OutputTokenCount, _lastUsage.TotalTokenCount);

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            // AIProjectClient does not implement IDisposable (verified via reflection on
            // Azure.AI.Projects assembly). No cleanup needed for _projectClient.
            _projectClient = null;
            _logger.LogDebug("AgentFrameworkService disposed");
        }
    }
}
