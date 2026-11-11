using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using WebApp.Api.Infrastructure;
using WebApp.Api.Models;
using WebApp.Api.Options;
using WebApp.Api.Security;
using WebApp.Api.Services;

// Local development only: load backend/WebApp.Api/.env (written by the azd postprovision hook).
// Variables already present in the environment win, so the file never overrides the platform.
LoadDotEnvFile(Path.Combine(Directory.GetCurrentDirectory(), ".env"));

var builder = WebApplication.CreateBuilder(args);

// Enable PII logging for debugging auth issues (ONLY IN DEVELOPMENT)
if (builder.Environment.IsDevelopment())
{
    Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;
}

// Add ServiceDefaults (telemetry, health checks)
builder.AddServiceDefaults();

// RFC 7807 ProblemDetails for every error, produced in one place (no internal details outside Development)
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddIngressForwardedHeaders();

// Input and usage limits (section "ChatLimits"), validated at startup.
builder.Services.AddOptions<ChatLimitsOptions>()
    .Bind(builder.Configuration.GetSection(ChatLimitsOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(o => o.MaxRequestBodyBytes > o.MaxTotalAttachmentBytes * 4 / 3,
        "ChatLimits:MaxRequestBodyBytes must exceed the base64 size of MaxTotalAttachmentBytes.")
    .ValidateOnStart();

// CORS: production serves the SPA from the same origin, so no cross-origin access is needed.
// Development allows the Vite dev server on localhost; other environments only explicitly configured origins.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy.SetIsOriginAllowed(origin =>
                    Uri.TryCreate(origin, UriKind.Absolute, out var uri) && (uri.Host == "localhost" || uri.Host == "127.0.0.1"))
                .AllowAnyMethod()
                .AllowAnyHeader();
        }
        else if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader();
        }
    });
});

// Override ClientId and TenantId from environment variables if provided
// These will be set by azd during deployment or by AppHost in local dev
var clientId = builder.Configuration["ENTRA_SPA_CLIENT_ID"]
    ?? builder.Configuration["AzureAd:ClientId"];

if (!string.IsNullOrEmpty(clientId))
{
    builder.Configuration["AzureAd:ClientId"] = clientId;
    // Set audience to match the expected token audience claim
    builder.Configuration["AzureAd:Audience"] = $"api://{clientId}";
}

var tenantId = builder.Configuration["ENTRA_TENANT_ID"]
    ?? builder.Configuration["AzureAd:TenantId"];

if (!string.IsNullOrEmpty(tenantId))
{
    builder.Configuration["AzureAd:TenantId"] = tenantId;
}

// Real delegated scope requested by the SPA (frontend/src/config/authConfig.ts) and exposed by the
// app registration (verified in lote 0).
const string RequiredScope = "Chat.ReadWrite";
const string ChatUserPolicy = "ChatUser";

// Add Microsoft Identity Web authentication
// Validates JWT bearer tokens issued for the SPA's delegated scope
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(options =>
    {
        builder.Configuration.Bind("AzureAd", options);
        var configuredClientId = builder.Configuration["AzureAd:ClientId"];
        var backendClientId = builder.Configuration["ENTRA_BACKEND_CLIENT_ID"];

        // When OBO is enabled, tokens are scoped to the backend API app
        var audiences = new List<string> { configuredClientId!, $"api://{configuredClientId}" };
        if (!string.IsNullOrEmpty(backendClientId))
        {
            audiences.Add(backendClientId);
            audiences.Add($"api://{backendClientId}");
        }
        options.TokenValidationParameters.ValidAudiences = audiences;

        options.TokenValidationParameters.NameClaimType = ClaimTypes.Name;
        options.TokenValidationParameters.RoleClaimType = ClaimTypes.Role;
    }, options => builder.Configuration.Bind("AzureAd", options));

// Any authenticated user of the CONSEIN tenant with the API scope may use Daily (no app roles).
// The same policy is the FallbackPolicy: every endpoint is protected unless it opts out
// explicitly with AllowAnonymous() (only health checks and the SPA shell do).
var chatUser = new AuthorizationPolicyBuilder()
    .RequireAuthenticatedUser()
    .RequireScope(RequiredScope)
    .RequireAssertion(context => UserIdentity.IsAllowedTenant(context.User, tenantId))
    .Build();

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(ChatUserPolicy, chatUser)
    .SetFallbackPolicy(chatUser);

// Per-user rate limits (partition = hashed owner key), evaluated after authentication.
builder.Services.AddApiRateLimiting();

// Ownership: resource-based authorization over Foundry conversation metadata.
builder.Services.AddSingleton<IUserIdentityAccessor, ClaimsUserIdentityAccessor>();
builder.Services.AddScoped<IAuthorizationHandler, ConversationOwnerAuthorizationHandler>();

// Register Foundry Agent Service (v2 Agents API)
builder.Services.AddHttpClient();
builder.Services.AddScoped<AgentFrameworkService>();
builder.Services.AddScoped<IAgentFrameworkService>(sp => sp.GetRequiredService<AgentFrameworkService>());
builder.Services.AddScoped<IConversationOwnershipStore>(sp => sp.GetRequiredService<AgentFrameworkService>());

var app = builder.Build();

// ---------- Pipeline ----------
// The ingress terminates TLS: read X-Forwarded-Proto/For first so HSTS and logs see the real request.
app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
// No UseHttpsRedirection: the ingress (allowInsecure: false) already redirects, and the platform
// health probes call the container over plain HTTP without X-Forwarded-Proto.

app.UseSecurityHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();

// Map health checks (anonymous, see ServiceDefaults)
app.MapDefaultEndpoints();

// Serve static files from wwwroot (frontend). Runs before authorization, so the SPA assets
// are outside the fallback policy.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseUserLogScope();

// Unauthenticated health endpoint for container probes
app.MapGet("/api/health", () => Results.Ok(new { status = "healthy" }))
    .AllowAnonymous()
    .DisableRateLimiting()
    .WithName("GetHealth");

var limits = app.Services.GetRequiredService<IOptions<ChatLimitsOptions>>().Value;
var api = app.MapGroup("/api")
    .RequireAuthorization(ChatUserPolicy)
    .RequireRateLimiting(RateLimitPolicies.Api);

// Streaming Chat endpoint: Streams agent response via SSE (conversationId → chunks → usage → done)
// Order: validate input → check ownership (or create the conversation) → start SSE. Validation and
// ownership failures are real HTTP 400/404 responses with no side effects (nothing created or uploaded).
api.MapPost("/chat/stream", async (
    ChatRequest request,
    IAgentFrameworkService agentService,
    IConversationOwnershipStore ownership,
    IAuthorizationService authorization,
    IUserIdentityAccessor identity,
    IOptions<ChatLimitsOptions> limitsOptions,
    HttpContext httpContext,
    IHostEnvironment environment,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    var ownerKey = identity.GetOwnerKey(httpContext.User);
    if (ownerKey is null)
    {
        return Results.Unauthorized();
    }

    // 1. Validate (no side effects yet)
    PreparedAttachments attachments;
    if (request.McpApproval is not null)
    {
        // An MCP approval always continues an existing conversation of the caller.
        if (string.IsNullOrWhiteSpace(request.ConversationId))
        {
            throw new RequestValidationException("An MCP approval must reference an existing conversation.");
        }
        attachments = PreparedAttachments.None;
    }
    else
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new RequestValidationException("Message cannot be empty.");
        }
        if (request.Message.Length > limitsOptions.Value.MaxMessageLength)
        {
            throw new RequestValidationException(
                $"Message is too long ({request.Message.Length:N0} characters); maximum {limitsOptions.Value.MaxMessageLength:N0}.");
        }
        attachments = AttachmentProcessor.Prepare(request.ImageDataUris, request.FileDataUris, limitsOptions.Value);
    }

    // 2. Ownership: sending, streaming, editing, regenerating and approving all continue this
    //    conversation, so only its owner may do it.
    if (!string.IsNullOrWhiteSpace(request.ConversationId)
        && !await IsConversationOwnerAsync(authorization, httpContext.User, request.ConversationId))
    {
        return ConversationNotFound();
    }

    var conversationId = string.IsNullOrWhiteSpace(request.ConversationId)
        ? await agentService.CreateConversationAsync(ownerKey, request.Message, cancellationToken)
        : request.ConversationId;

    // 3. Stream
    var response = httpContext.Response;
    response.ContentType = "text/event-stream";
    response.Headers.CacheControl = "no-cache";
    response.Headers["X-Accel-Buffering"] = "no";

    try
    {
        await WriteConversationIdEvent(response, conversationId, cancellationToken);

        var stopwatch = Stopwatch.StartNew();
        var citedFiles = new List<AuthorizedFile>();

        await foreach (var chunk in agentService.StreamMessageAsync(conversationId, request.Message, attachments, request.McpApproval, cancellationToken))
        {
            if (chunk.IsText && chunk.TextDelta != null)
            {
                await WriteChunkEvent(response, chunk.TextDelta, cancellationToken);
            }
            else if (chunk.HasAnnotations && chunk.Annotations != null)
            {
                citedFiles.AddRange(chunk.Annotations
                    .Where(a => !string.IsNullOrEmpty(a.FileId))
                    .Select(a => new AuthorizedFile(a.FileId!, string.IsNullOrEmpty(a.ContainerId) ? null : a.ContainerId)));
                await WriteAnnotationsEvent(response, chunk.Annotations, cancellationToken);
            }
            else if (chunk.IsMcpApprovalRequest && chunk.McpApprovalRequest != null)
            {
                await WriteMcpApprovalRequestEvent(response, chunk.McpApprovalRequest, cancellationToken);
            }
            else if (chunk.IsToolUse && chunk.ToolName != null)
            {
                await WriteToolUseEvent(response, chunk.ToolName, cancellationToken);
            }
        }

        // Files cited or generated here become downloadable by the conversation owner.
        if (citedFiles.Count > 0)
        {
            try
            {
                await ownership.AddAuthorizedFilesAsync(conversationId, citedFiles, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Could not record authorized files for conversation {ConversationId}; they will not be downloadable", conversationId);
            }
        }

        var usage = agentService.GetLastUsage();
        await WriteUsageEvent(response, stopwatch.Elapsed.TotalMilliseconds,
            usage?.InputTokens ?? 0, usage?.OutputTokens ?? 0, usage?.TotalTokens ?? 0, cancellationToken);
        await WriteDoneEvent(response, cancellationToken);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        // The user pressed "stop" or closed the tab: not an error.
        logger.LogDebug("Chat stream for conversation {ConversationId} cancelled by the client", conversationId);
    }
    catch (Exception ex)
    {
        // The SSE response already started: report a sanitized error event.
        var status = ApiExceptionHandler.MapStatusCode(ex);
        logger.LogError(ex, "Chat stream failed for conversation {ConversationId} ({Status})", conversationId, status);
        var problem = ApiExceptionHandler.CreateProblemDetails(ex, status, environment.IsDevelopment());
        await WriteErrorEvent(response, problem.Detail ?? problem.Title ?? "Error", CancellationToken.None);
    }

    return Results.Empty;
})
.RequireRateLimiting(RateLimitPolicies.Chat)
.WithMetadata(new RequestSizeLimitAttribute(limits.MaxRequestBodyBytes))
.WithName("StreamChatMessage");

// Public agent metadata (name, description, model, starter prompts, logo). Never the instructions.
api.MapGet("/agent", async (IAgentFrameworkService agentService, CancellationToken cancellationToken) =>
        Results.Ok(await agentService.GetAgentMetadataAsync(cancellationToken)))
    .WithName("GetAgentMetadata");

// Agent name (debug)
api.MapGet("/agent/info", async (IAgentFrameworkService agentService, CancellationToken cancellationToken) =>
        Results.Ok(new { info = await agentService.GetAgentInfoAsync(cancellationToken), status = "ready" }))
    .WithName("GetAgentInfo");

// List the caller's conversations (filtered by owner; conversations without owner are not listed)
api.MapGet("/conversations", async (
    IAgentFrameworkService agentService,
    IUserIdentityAccessor identity,
    HttpContext httpContext,
    int? limit,
    CancellationToken cancellationToken) =>
{
    var ownerKey = identity.GetOwnerKey(httpContext.User);
    if (ownerKey is null)
    {
        return Results.Unauthorized();
    }

    var pageSize = Math.Clamp(limit ?? 20, 1, 100);
    var conversations = await agentService.ListConversationsAsync(ownerKey, pageSize, cancellationToken);
    var hasMore = conversations.Count > pageSize;
    return Results.Ok(new { conversations = hasMore ? conversations.Take(pageSize).ToList() : conversations, hasMore });
})
.WithName("ListConversations");

// Get conversation messages (owner only)
api.MapGet("/conversations/{conversationId}/messages", async (
    string conversationId,
    IAgentFrameworkService agentService,
    IAuthorizationService authorization,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    if (!await IsConversationOwnerAsync(authorization, httpContext.User, conversationId))
    {
        return ConversationNotFound();
    }

    return Results.Ok(await agentService.GetConversationMessagesAsync(conversationId, cancellationToken));
})
.WithName("GetConversationMessages");

// Delete conversation (owner only)
api.MapDelete("/conversations/{conversationId}", async (
    string conversationId,
    IAgentFrameworkService agentService,
    IAuthorizationService authorization,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    if (!await IsConversationOwnerAsync(authorization, httpContext.User, conversationId))
    {
        return ConversationNotFound();
    }

    try
    {
        await agentService.DeleteConversationAsync(conversationId, cancellationToken);
        return Results.NoContent();
    }
    catch (NotSupportedException)
    {
        return Results.Problem(
            title: "Not Implemented",
            detail: "Conversation deletion is not yet supported by the Azure.AI.Projects SDK.",
            statusCode: StatusCodes.Status501NotImplemented);
    }
})
.WithName("DeleteConversation");

// Download a file cited or generated in one of the caller's conversations.
// The managed identity can read every file of the Foundry project, so the file id alone is not
// authorization: the caller must own the conversation and the file must be recorded in it.
api.MapGet("/files/{fileId}", async (
    string fileId,
    string? conversationId,
    string? containerId,
    IAgentFrameworkService agentService,
    IConversationOwnershipStore ownership,
    IAuthorizationService authorization,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(conversationId))
    {
        throw new RequestValidationException("The conversationId query parameter is required.");
    }

    if (!await IsConversationOwnerAsync(authorization, httpContext.User, conversationId)
        || !await ownership.IsFileAuthorizedAsync(conversationId, fileId, containerId, cancellationToken))
    {
        return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Not Found", detail: "File not found.");
    }

    var (content, fileName) = await agentService.DownloadFileAsync(fileId, containerId, cancellationToken);
    // Results.File with a file name sends Content-Disposition: attachment.
    return Results.File(content.ToArray(), GetSafeContentType(fileName), fileName);
})
.WithName("DownloadFile");

// Unknown /api routes return 404 ProblemDetails instead of falling through to the SPA's index.html.
api.MapFallback(() => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Not Found"));

// Fallback route for SPA - serve index.html for any non-API routes (public shell; data needs a token)
app.MapFallbackToFile("index.html").AllowAnonymous();

app.Run();

// ---------- Helpers ----------

// Resource-based ownership check (see ConversationOwnerAuthorizationHandler).
static async Task<bool> IsConversationOwnerAsync(IAuthorizationService authorization, ClaimsPrincipal user, string conversationId) =>
    (await authorization.AuthorizeAsync(user, conversationId, ConversationOwnerRequirement.Instance)).Succeeded;

// Same response whether the conversation does not exist or belongs to someone else.
static IResult ConversationNotFound() =>
    Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Not Found", detail: "Conversation not found.");

// Model-generated content is untrusted: types the browser could execute or render as a document
// (HTML, SVG, JavaScript, XML) are served as application/octet-stream, never active on this origin.
static string GetSafeContentType(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
{
    ".png" => "image/png",
    ".jpg" or ".jpeg" => "image/jpeg",
    ".gif" => "image/gif",
    ".webp" => "image/webp",
    ".pdf" => "application/pdf",
    ".csv" => "text/csv",
    ".json" => "application/json",
    ".txt" or ".md" or ".py" => "text/plain",
    _ => "application/octet-stream",
};

static void LoadDotEnvFile(string path)
{
    if (!File.Exists(path))
    {
        return;
    }

    foreach (var rawLine in File.ReadAllLines(path))
    {
        var line = rawLine.Trim();
        if (line.Length == 0 || line.StartsWith('#'))
        {
            continue;
        }

        var parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 2 && parts[0].Length > 0 && Environment.GetEnvironmentVariable(parts[0]) is null)
        {
            Environment.SetEnvironmentVariable(parts[0], parts[1].Trim('"'));
        }
    }
}

static async Task WriteSseAsync(HttpResponse response, object payload, CancellationToken ct)
{
    var json = System.Text.Json.JsonSerializer.Serialize(payload);
    await response.WriteAsync($"data: {json}\n\n", ct);
    await response.Body.FlushAsync(ct);
}

static Task WriteConversationIdEvent(HttpResponse response, string conversationId, CancellationToken ct) =>
    WriteSseAsync(response, new { type = "conversationId", conversationId }, ct);

static Task WriteChunkEvent(HttpResponse response, string content, CancellationToken ct) =>
    WriteSseAsync(response, new { type = "chunk", content }, ct);

static Task WriteToolUseEvent(HttpResponse response, string toolName, CancellationToken ct) =>
    WriteSseAsync(response, new { type = "toolUse", toolName }, ct);

static Task WriteAnnotationsEvent(HttpResponse response, List<AnnotationInfo> annotations, CancellationToken ct) =>
    WriteSseAsync(response, new
    {
        type = "annotations",
        annotations = annotations.Select(a => new
        {
            type = a.Type,
            label = a.Label,
            url = a.Url,
            fileId = a.FileId,
            containerId = a.ContainerId,
            textToReplace = a.TextToReplace,
            startIndex = a.StartIndex,
            endIndex = a.EndIndex,
            quote = a.Quote
        })
    }, ct);

static Task WriteMcpApprovalRequestEvent(HttpResponse response, McpApprovalRequest approval, CancellationToken ct) =>
    WriteSseAsync(response, new
    {
        type = "mcpApprovalRequest",
        approvalRequest = new
        {
            id = approval.Id,
            toolName = approval.ToolName,
            serverLabel = approval.ServerLabel,
            arguments = approval.Arguments,
            previousResponseId = approval.PreviousResponseId
        }
    }, ct);

static Task WriteUsageEvent(HttpResponse response, double duration, int promptTokens, int completionTokens, int totalTokens, CancellationToken ct) =>
    WriteSseAsync(response, new { type = "usage", duration, promptTokens, completionTokens, totalTokens }, ct);

static Task WriteDoneEvent(HttpResponse response, CancellationToken ct) =>
    WriteSseAsync(response, new { type = "done" }, ct);

static Task WriteErrorEvent(HttpResponse response, string message, CancellationToken ct) =>
    WriteSseAsync(response, new { type = "error", message }, ct);

/// <summary>Exposed for WebApplicationFactory-based integration tests.</summary>
public partial class Program;
