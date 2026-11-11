using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;
using WebApp.Api.Models;
using WebApp.Api.Security;
using WebApp.Api.Services;
using System.Security.Claims;

// Load .env file for local development BEFORE building the configuration
// In production (Docker), Container Apps injects environment variables directly
var envFilePath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (File.Exists(envFilePath))
{
    foreach (var line in File.ReadAllLines(envFilePath))
    {
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
            continue;

        var parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 2)
        {
            // Set as environment variables so they're picked up by configuration system
            Environment.SetEnvironmentVariable(parts[0], parts[1]);
        }
    }
}

var builder = WebApplication.CreateBuilder(args);

// Enable PII logging for debugging auth issues (ONLY IN DEVELOPMENT)
if (builder.Environment.IsDevelopment())
{
    Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;
}

// Add ServiceDefaults (telemetry, health checks)
builder.AddServiceDefaults();

// Add ProblemDetails service for standardized RFC 7807 error responses
builder.Services.AddProblemDetails();

// Register IHttpContextAccessor for services that need access to the current HTTP request
builder.Services.AddHttpContextAccessor();

// Configure CORS for local development and production
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:8080" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        // In development, allow any localhost port for flexibility
        if (builder.Environment.IsDevelopment())
        {
            policy.SetIsOriginAllowed(origin =>
            {
                if (Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                {
                    return uri.Host == "localhost" || uri.Host == "127.0.0.1";
                }
                return false;
            })
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
        }
        else
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
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

// Ownership: resource-based authorization over Foundry conversation metadata.
builder.Services.AddSingleton<IUserIdentityAccessor, ClaimsUserIdentityAccessor>();
builder.Services.AddScoped<IAuthorizationHandler, ConversationOwnerAuthorizationHandler>();

// Register Foundry Agent Service (v2 Agents API)
// Uses Azure.AI.Projects SDK which works with v2 Agents API (/agents/ endpoint with human-readable IDs).
builder.Services.AddHttpClient();
builder.Services.AddScoped<AgentFrameworkService>();
builder.Services.AddScoped<IAgentFrameworkService>(sp => sp.GetRequiredService<AgentFrameworkService>());
builder.Services.AddScoped<IConversationOwnershipStore>(sp => sp.GetRequiredService<AgentFrameworkService>());

var app = builder.Build();

// Add exception handling middleware for production
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
}

// Add status code pages for consistent error responses
app.UseStatusCodePages();

// Map health checks (anonymous, see ServiceDefaults)
app.MapDefaultEndpoints();

// Serve static files from wwwroot (frontend). Runs before authorization, so the SPA assets
// are outside the fallback policy.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors("AllowFrontend");

// Note: HTTPS redirection not needed - Azure Container Apps handles SSL termination at ingress
// The container receives HTTP traffic on port 8080

// Add authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// Unauthenticated health endpoint for container probes
app.MapGet("/api/health", () => Results.Ok(new { status = "healthy" }))
    .AllowAnonymous()
    .WithName("GetHealth");

var api = app.MapGroup("/api").RequireAuthorization(ChatUserPolicy);

// Streaming Chat endpoint: Streams agent response via SSE (conversationId → chunks → usage → done)
// Supports MCP tool approval flow with previousResponseId and mcpApproval parameters
api.MapPost("/chat/stream", async (
    ChatRequest request,
    IAgentFrameworkService agentService,
    IConversationOwnershipStore ownership,
    IAuthorizationService authorization,
    IUserIdentityAccessor identity,
    HttpContext httpContext,
    IHostEnvironment environment,
    CancellationToken cancellationToken) =>
{
    var ownerKey = identity.GetOwnerKey(httpContext.User);
    if (ownerKey is null)
    {
        return Results.Unauthorized();
    }

    // An MCP approval always continues an existing conversation of the caller.
    if (request.McpApproval is not null && string.IsNullOrWhiteSpace(request.ConversationId))
    {
        return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid Request",
            detail: "An MCP approval must reference an existing conversation.");
    }

    // Sending, streaming, editing, regenerating and approving all target this conversation:
    // only its owner may continue it. Checked before the SSE response starts.
    if (!string.IsNullOrWhiteSpace(request.ConversationId)
        && !await IsConversationOwnerAsync(authorization, httpContext.User, request.ConversationId))
    {
        return ConversationNotFound();
    }

    try
    {
        httpContext.Response.Headers.Append("Content-Type", "text/event-stream");
        httpContext.Response.Headers.Append("Cache-Control", "no-cache");
        httpContext.Response.Headers.Append("Connection", "keep-alive");

        var conversationId = string.IsNullOrWhiteSpace(request.ConversationId)
            ? await agentService.CreateConversationAsync(ownerKey, request.Message, cancellationToken)
            : request.ConversationId;

        await WriteConversationIdEvent(httpContext.Response, conversationId, cancellationToken);

        var startTime = DateTime.UtcNow;
        var citedFiles = new List<AuthorizedFile>();

        await foreach (var chunk in agentService.StreamMessageAsync(
            conversationId,
            request.Message,
            request.ImageDataUris,
            request.FileDataUris,
            request.PreviousResponseId,
            request.McpApproval,
            cancellationToken))
        {
            if (chunk.IsText && chunk.TextDelta != null)
            {
                await WriteChunkEvent(httpContext.Response, chunk.TextDelta, cancellationToken);
            }
            else if (chunk.HasAnnotations && chunk.Annotations != null)
            {
                citedFiles.AddRange(chunk.Annotations
                    .Where(a => !string.IsNullOrEmpty(a.FileId))
                    .Select(a => new AuthorizedFile(a.FileId!, string.IsNullOrEmpty(a.ContainerId) ? null : a.ContainerId)));
                await WriteAnnotationsEvent(httpContext.Response, chunk.Annotations, cancellationToken);
            }
            else if (chunk.IsMcpApprovalRequest && chunk.McpApprovalRequest != null)
            {
                await WriteMcpApprovalRequestEvent(httpContext.Response, chunk.McpApprovalRequest, cancellationToken);
            }
            else if (chunk.IsToolUse && chunk.ToolName != null)
            {
                await WriteToolUseEvent(httpContext.Response, chunk.ToolName, cancellationToken);
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
                var logger = httpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogWarning(ex, "Could not record authorized files for conversation {ConversationId}; they will not be downloadable", conversationId);
            }
        }

        var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
        var usage = agentService.GetLastUsage();
        await WriteUsageEvent(
            httpContext.Response,
            duration,
            usage?.InputTokens ?? 0,
            usage?.OutputTokens ?? 0,
            usage?.TotalTokens ?? 0,
            cancellationToken);

        await WriteDoneEvent(httpContext.Response, cancellationToken);
    }
    catch (ArgumentException ex) when (ex.Message.Contains("Invalid") && (ex.Message.Contains("attachments") || ex.Message.Contains("image") || ex.Message.Contains("file")))
    {
        // Validation errors from image/file processing - return 400 Bad Request
        var errorResponse = ErrorResponseFactory.CreateFromException(
            ex,
            400,
            environment.IsDevelopment());

        await WriteErrorEvent(
            httpContext.Response,
            errorResponse.Detail ?? errorResponse.Title,
            cancellationToken);
    }
    catch (Exception ex)
    {
        var logger = httpContext.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Chat stream error: {Message}", ex.Message);

        var errorResponse = ErrorResponseFactory.CreateFromException(
            ex,
            500,
            environment.IsDevelopment());

        await WriteErrorEvent(
            httpContext.Response,
            errorResponse.Detail ?? errorResponse.Title,
            cancellationToken);
    }

    return Results.Empty;
})
.WithName("StreamChatMessage");

static async Task WriteConversationIdEvent(HttpResponse response, string conversationId, CancellationToken ct)
{
    var json = System.Text.Json.JsonSerializer.Serialize(new { type = "conversationId", conversationId });
    await response.WriteAsync($"data: {json}\n\n", ct);
    await response.Body.FlushAsync(ct);
}

static async Task WriteChunkEvent(HttpResponse response, string content, CancellationToken ct)
{
    var json = System.Text.Json.JsonSerializer.Serialize(new { type = "chunk", content });
    await response.WriteAsync($"data: {json}\n\n", ct);
    await response.Body.FlushAsync(ct);
}

static async Task WriteToolUseEvent(HttpResponse response, string toolName, CancellationToken ct)
{
    var json = System.Text.Json.JsonSerializer.Serialize(new { type = "toolUse", toolName });
    await response.WriteAsync($"data: {json}\n\n", ct);
    await response.Body.FlushAsync(ct);
}

static async Task WriteAnnotationsEvent(HttpResponse response, List<WebApp.Api.Models.AnnotationInfo> annotations, CancellationToken ct)
{
    var json = System.Text.Json.JsonSerializer.Serialize(new
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
    });
    await response.WriteAsync($"data: {json}\n\n", ct);
    await response.Body.FlushAsync(ct);
}

static async Task WriteMcpApprovalRequestEvent(HttpResponse response, WebApp.Api.Models.McpApprovalRequest approval, CancellationToken ct)
{
    var json = System.Text.Json.JsonSerializer.Serialize(new
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
    });
    await response.WriteAsync($"data: {json}\n\n", ct);
    await response.Body.FlushAsync(ct);
}

static async Task WriteUsageEvent(HttpResponse response, double duration, int promptTokens, int completionTokens, int totalTokens, CancellationToken ct)
{
    var json = System.Text.Json.JsonSerializer.Serialize(new
    {
        type = "usage",
        duration,
        promptTokens,
        completionTokens,
        totalTokens
    });
    await response.WriteAsync($"data: {json}\n\n", ct);
    await response.Body.FlushAsync(ct);
}

static async Task WriteDoneEvent(HttpResponse response, CancellationToken ct)
{
    await response.WriteAsync("data: {\"type\":\"done\"}\n\n", ct);
    await response.Body.FlushAsync(ct);
}

static async Task WriteErrorEvent(HttpResponse response, string message, CancellationToken ct)
{
    var json = System.Text.Json.JsonSerializer.Serialize(new { type = "error", message });
    await response.WriteAsync($"data: {json}\n\n", ct);
    await response.Body.FlushAsync(ct);
}

// Get agent metadata (name, description, model, metadata)
// Used by frontend to display agent information in the UI
api.MapGet("/agent", async (
    IAgentFrameworkService agentService,
    IHostEnvironment environment,
    CancellationToken cancellationToken) =>
{
    try
    {
        var metadata = await agentService.GetAgentMetadataAsync(cancellationToken);
        return Results.Ok(metadata);
    }
    catch (Exception ex)
    {
        var errorResponse = ErrorResponseFactory.CreateFromException(
            ex,
            500,
            environment.IsDevelopment());

        return Results.Problem(
            title: errorResponse.Title,
            detail: errorResponse.Detail,
            statusCode: errorResponse.Status,
            extensions: errorResponse.Extensions
        );
    }
})
.WithName("GetAgentMetadata");

// Get agent info (for debugging)
api.MapGet("/agent/info", async (
    IAgentFrameworkService agentService,
    IHostEnvironment environment,
    CancellationToken cancellationToken) =>
{
    try
    {
        var agentInfo = await agentService.GetAgentInfoAsync(cancellationToken);
        return Results.Ok(new
        {
            info = agentInfo,
            status = "ready"
        });
    }
    catch (Exception ex)
    {
        var errorResponse = ErrorResponseFactory.CreateFromException(
            ex,
            500,
            environment.IsDevelopment());

        return Results.Problem(
            title: errorResponse.Title,
            detail: errorResponse.Detail,
            statusCode: errorResponse.Status,
            extensions: errorResponse.Extensions
        );
    }
})
.WithName("GetAgentInfo");

// List the caller's conversations (filtered by owner; conversations without owner are not listed)
api.MapGet("/conversations", async (
    IAgentFrameworkService agentService,
    IUserIdentityAccessor identity,
    HttpContext httpContext,
    IHostEnvironment environment,
    int? limit,
    CancellationToken cancellationToken) =>
{
    var ownerKey = identity.GetOwnerKey(httpContext.User);
    if (ownerKey is null)
    {
        return Results.Unauthorized();
    }

    try
    {
        var pageSize = Math.Clamp(limit ?? 20, 1, 100);
        var conversations = await agentService.ListConversationsAsync(ownerKey, pageSize, cancellationToken);
        var hasMore = conversations.Count > pageSize;
        if (hasMore)
            conversations = conversations.Take(pageSize).ToList();
        return Results.Ok(new { conversations, hasMore });
    }
    catch (Exception ex)
    {
        var errorResponse = ErrorResponseFactory.CreateFromException(ex, 500, environment.IsDevelopment());
        return Results.Problem(
            title: errorResponse.Title,
            detail: errorResponse.Detail,
            statusCode: errorResponse.Status,
            extensions: errorResponse.Extensions
        );
    }
})
.WithName("ListConversations");

// Get conversation messages (owner only)
api.MapGet("/conversations/{conversationId}/messages", async (
    string conversationId,
    IAgentFrameworkService agentService,
    IAuthorizationService authorization,
    HttpContext httpContext,
    IHostEnvironment environment,
    CancellationToken cancellationToken) =>
{
    if (!await IsConversationOwnerAsync(authorization, httpContext.User, conversationId))
    {
        return ConversationNotFound();
    }

    try
    {
        var messages = await agentService.GetConversationMessagesAsync(conversationId, cancellationToken);
        return Results.Ok(messages);
    }
    catch (Exception ex)
    {
        var errorResponse = ErrorResponseFactory.CreateFromException(ex, 500, environment.IsDevelopment());
        return Results.Problem(
            title: errorResponse.Title,
            detail: errorResponse.Detail,
            statusCode: errorResponse.Status,
            extensions: errorResponse.Extensions
        );
    }
})
.WithName("GetConversationMessages");

// Delete conversation (owner only)
api.MapDelete("/conversations/{conversationId}", async (
    string conversationId,
    IAgentFrameworkService agentService,
    IAuthorizationService authorization,
    HttpContext httpContext,
    IHostEnvironment environment,
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
            statusCode: 501
        );
    }
    catch (Exception ex)
    {
        var errorResponse = ErrorResponseFactory.CreateFromException(ex, 500, environment.IsDevelopment());
        return Results.Problem(
            title: errorResponse.Title,
            detail: errorResponse.Detail,
            statusCode: errorResponse.Status,
            extensions: errorResponse.Extensions
        );
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
    IHostEnvironment environment,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(conversationId))
    {
        return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid Request",
            detail: "The conversationId query parameter is required.");
    }

    if (!await IsConversationOwnerAsync(authorization, httpContext.User, conversationId)
        || !await ownership.IsFileAuthorizedAsync(conversationId, fileId, containerId, cancellationToken))
    {
        return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Not Found", detail: "File not found.");
    }

    try
    {
        var (content, fileName) = await agentService.DownloadFileAsync(fileId, containerId, cancellationToken);
        var contentType = GetMimeType(fileName);
        return Results.File(content.ToArray(), contentType, fileName);
    }
    catch (HttpRequestException httpEx)
    {
        var statusCode = (int?)httpEx.StatusCode ?? 502;
        var errorResponse = ErrorResponseFactory.CreateFromException(httpEx, statusCode, environment.IsDevelopment());
        return Results.Problem(
            title: errorResponse.Title,
            detail: errorResponse.Detail,
            statusCode: errorResponse.Status,
            extensions: errorResponse.Extensions
        );
    }
    catch (Exception ex)
    {
        var errorResponse = ErrorResponseFactory.CreateFromException(ex, 500, environment.IsDevelopment());
        return Results.Problem(
            title: errorResponse.Title,
            detail: errorResponse.Detail,
            statusCode: errorResponse.Status,
            extensions: errorResponse.Extensions
        );
    }
})
.WithName("DownloadFile");

// Unknown /api routes return 404 ProblemDetails instead of falling through to the SPA's index.html.
api.MapFallback(() => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Not Found"));

// Fallback route for SPA - serve index.html for any non-API routes (public shell; data needs a token)
app.MapFallbackToFile("index.html").AllowAnonymous();

app.Run();

// Resource-based ownership check (see ConversationOwnerAuthorizationHandler).
static async Task<bool> IsConversationOwnerAsync(IAuthorizationService authorization, ClaimsPrincipal user, string conversationId) =>
    (await authorization.AuthorizeAsync(user, conversationId, ConversationOwnerRequirement.Instance)).Succeeded;

// Same response whether the conversation does not exist or belongs to someone else.
static IResult ConversationNotFound() =>
    Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Not Found", detail: "Conversation not found.");

// Helper to determine MIME type from file extension
static string GetMimeType(string fileName)
{
    var ext = Path.GetExtension(fileName).ToLowerInvariant();
    return ext switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".svg" => "image/svg+xml",
        ".pdf" => "application/pdf",
        ".csv" => "text/csv",
        ".json" => "application/json",
        ".txt" => "text/plain",
        ".md" => "text/markdown",
        ".html" => "text/html",
        ".py" => "text/x-python",
        ".js" => "text/javascript",
        _ => "application/octet-stream",
    };
}

/// <summary>Exposed for WebApplicationFactory-based integration tests.</summary>
public partial class Program;
