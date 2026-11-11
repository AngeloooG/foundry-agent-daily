using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using WebApp.Api.Security;
using WebApp.Api.Tests.TestSupport;

namespace WebApp.Api.Tests;

/// <summary>
/// Integration tests of the critical API routes, hosted in memory (WebApplicationFactory) with a
/// fake Foundry. Created in Lote 1, BEFORE any refactor:
/// - Active tests characterize behavior that must be preserved.
/// - [Ignore] tests define the target behavior of later lotes; each lote removes its Ignore.
/// </summary>
[TestClass]
public sealed class CriticalRoutesIntegrationTests
{
    // Pseudonymous test identities (not real users).
    private const string UserA = "aaaaaaaa-0000-4000-8000-00000000000a";
    private const string UserB = "bbbbbbbb-0000-4000-8000-00000000000b";
    private const string OtherTenant = "99999999-9999-4999-8999-999999999999";

    private ApiFactory _factory = null!;

    [TestInitialize]
    public void Initialize() => _factory = new ApiFactory();

    [TestCleanup]
    public void Cleanup() => _factory.Dispose();

    // ===================== Preserved behavior (active) =====================

    [TestMethod]
    [DataRow("/api/health")]
    [DataRow("/health")]
    [DataRow("/alive")]
    public async Task HealthChecks_AreAnonymous(string url)
    {
        var response = await _factory.CreateClient().GetAsync(url);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    [DataRow("GET", "/api/agent")]
    [DataRow("GET", "/api/conversations")]
    [DataRow("GET", "/api/conversations/conv_0001/messages")]
    [DataRow("POST", "/api/chat/stream")]
    [DataRow("GET", "/api/files/file_1")]
    public async Task ApiRoutes_RequireAuthentication(string method, string url)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method == "POST")
        {
            request.Content = JsonContent.Create(new { message = "hola" });
        }

        var response = await _factory.CreateClient().SendAsync(request);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.AreEqual(0, _factory.Foundry.StreamCalls);
    }

    [TestMethod]
    public async Task ApiRoutes_RequireChatReadWriteScope()
    {
        var response = await _factory.CreateUserClient(UserA, scopes: "User.Read").GetAsync("/api/conversations");
        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task ChatStream_NewConversation_FollowsSseContract()
    {
        var (response, events) = await _factory.CreateUserClient(UserA).PostChatAsync(new { message = "Hola Daily" });

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("text/event-stream", response.Content.Headers.ContentType?.MediaType);
        CollectionAssert.AreEqual(
            new[] { "conversationId", "chunk", "chunk", "annotations", "usage", "done" },
            events.Types());
        Assert.AreEqual("Hola", events[1].GetProperty("content").GetString());
        Assert.AreEqual(15, events[4].GetProperty("totalTokens").GetInt32());
        Assert.AreEqual(1, _factory.Foundry.ConversationIds.Count);
    }

    [TestMethod]
    public async Task ChatStream_ExistingConversation_IsReusedNotDuplicated()
    {
        var client = _factory.CreateUserClient(UserA);
        var conversationId = await StartConversationAsync(client);

        var (_, events) = await client.PostChatAsync(new { message = "segunda", conversationId });

        Assert.AreEqual(conversationId, events[0].GetProperty("conversationId").GetString());
        Assert.AreEqual(1, _factory.Foundry.ConversationIds.Count);
    }

    [TestMethod]
    public async Task Owner_CanReadOwnConversation()
    {
        var client = _factory.CreateUserClient(UserA);
        var conversationId = await StartConversationAsync(client);

        var response = await client.GetAsync($"/api/conversations/{conversationId}/messages");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task AgentMetadata_ReturnsNameAndStarterPrompts()
    {
        var json = await _factory.CreateUserClient(UserA).GetFromJsonAsync<JsonElement>("/api/agent");

        Assert.AreEqual("Daily", json.GetProperty("name").GetString());
        Assert.AreEqual("Hola", json.GetProperty("starterPrompts")[0].GetString());
    }

    // ===================== Lote 2: ownership (target behavior) =====================

    [TestMethod]
    public async Task OtherUser_GetsNotFound_WhenReadingMessages()
    {
        var conversationId = await StartConversationAsync(_factory.CreateUserClient(UserA));

        var response = await _factory.CreateUserClient(UserB).GetAsync($"/api/conversations/{conversationId}/messages");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task OtherUser_GetsNotFound_WhenSendingOrStreaming()
    {
        var conversationId = await StartConversationAsync(_factory.CreateUserClient(UserA));
        var streamsBefore = _factory.Foundry.StreamCalls;

        var (response, events) = await _factory.CreateUserClient(UserB).PostChatAsync(new { message = "inyección", conversationId });

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.AreEqual(0, events.Count);
        Assert.AreEqual(streamsBefore, _factory.Foundry.StreamCalls, "No stream may start on a conversation the caller does not own.");
    }

    [TestMethod]
    public async Task OtherUser_CannotApproveMcpCall()
    {
        var conversationId = await StartConversationAsync(_factory.CreateUserClient(UserA));

        var (response, _) = await _factory.CreateUserClient(UserB).PostChatAsync(new
        {
            message = "Approved",
            conversationId,
            previousResponseId = "resp_1",
            mcpApproval = new { approvalRequestId = "mcpr_1", approved = true }
        });

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.IsNull(_factory.Foundry.LastApproval);
    }

    [TestMethod]
    public async Task OtherUser_CannotDownloadFilesOfAnotherUsersConversation()
    {
        var conversationId = await StartConversationAsync(_factory.CreateUserClient(UserA));

        var response = await _factory.CreateUserClient(UserB).GetAsync($"/api/files/file_cited?conversationId={conversationId}");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.AreEqual(0, _factory.Foundry.DownloadCalls);
    }

    [TestMethod]
    public async Task FileDownload_WithoutConversationId_IsRejected()
    {
        var response = await _factory.CreateUserClient(UserA).GetAsync("/api/files/file_cited");
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.AreEqual(0, _factory.Foundry.DownloadCalls);
    }

    [TestMethod]
    public async Task List_ShowsOnlyOwnConversations_AndHidesLegacyOnes()
    {
        var conversationOfA = await StartConversationAsync(_factory.CreateUserClient(UserA));
        var conversationOfB = await StartConversationAsync(_factory.CreateUserClient(UserB));
        _factory.Foundry.SeedLegacyConversation("conv_legacy", "creada antes del ownership");

        var json = await _factory.CreateUserClient(UserB).GetFromJsonAsync<JsonElement>("/api/conversations");
        var ids = json.GetProperty("conversations").EnumerateArray().Select(c => c.GetProperty("id").GetString()).ToList();

        CollectionAssert.AreEqual(new[] { conversationOfB }, ids);
        CollectionAssert.DoesNotContain(ids, conversationOfA);
        CollectionAssert.DoesNotContain(ids, "conv_legacy");
    }

    [TestMethod]
    public async Task NewConversation_StoresHashedOwner_NeverClearIds()
    {
        var conversationId = await StartConversationAsync(_factory.CreateUserClient(UserA));
        var metadata = _factory.Foundry.MetadataOf(conversationId);

        Assert.AreEqual(UserIdentity.ComputeOwnerKey(TestAuthHandler.HomeTenantId, UserA), metadata[ConversationMetadata.OwnerKey]);
        Assert.IsFalse(metadata.Values.Any(v => v.Contains(UserA, StringComparison.OrdinalIgnoreCase)), "oid must not be stored in clear");
        Assert.IsFalse(metadata.Values.Any(v => v.Contains(TestAuthHandler.HomeTenantId, StringComparison.OrdinalIgnoreCase)), "tid must not be stored in clear");
    }

    [TestMethod]
    public async Task Owner_CanDownloadFileCitedInOwnConversation()
    {
        var client = _factory.CreateUserClient(UserA);
        var conversationId = await StartConversationAsync(client); // the fake cites FakeFoundry.CitedFileId

        var response = await client.GetAsync($"/api/files/{FakeFoundry.CitedFileId}?conversationId={conversationId}");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(1, _factory.Foundry.DownloadCalls);
    }

    [TestMethod]
    public async Task Owner_CannotDownloadFileNotCitedInTheConversation()
    {
        var client = _factory.CreateUserClient(UserA);
        var conversationId = await StartConversationAsync(client);

        var response = await client.GetAsync($"/api/files/file_from_another_project?conversationId={conversationId}");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.AreEqual(0, _factory.Foundry.DownloadCalls);
    }

    [TestMethod]
    public async Task Owner_CanApproveOwnMcpCall()
    {
        var client = _factory.CreateUserClient(UserA);
        var conversationId = await StartConversationAsync(client);

        var (response, _) = await client.PostChatAsync(new
        {
            message = "Approved",
            conversationId,
            previousResponseId = "resp_1",
            mcpApproval = new { approvalRequestId = "mcpr_1", approved = true }
        });

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("mcpr_1", _factory.Foundry.LastApproval?.ApprovalRequestId);
    }

    [TestMethod]
    public async Task McpApproval_WithoutConversation_IsRejected()
    {
        var (response, _) = await _factory.CreateUserClient(UserA).PostChatAsync(new
        {
            message = "Approved",
            mcpApproval = new { approvalRequestId = "mcpr_1", approved = true }
        });

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.AreEqual(0, _factory.Foundry.ConversationIds.Count);
    }

    [TestMethod]
    public async Task OtherUser_GetsNotFound_WhenDeleting()
    {
        var conversationId = await StartConversationAsync(_factory.CreateUserClient(UserA));

        var response = await _factory.CreateUserClient(UserB).DeleteAsync($"/api/conversations/{conversationId}");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    [DataRow("POST", "/api/files/cleanup")]
    [DataRow("GET", "/api/files/uploaded")]
    public async Task AdminFileOperations_AreNotExposed(string method, string url)
    {
        var response = await _factory.CreateUserClient(UserA).SendAsync(new HttpRequestMessage(new HttpMethod(method), url));

        // No listing/cleanup operation exists any more. GET /api/files/uploaded now only matches the
        // download route (fileId="uploaded") and is rejected for lacking a conversationId.
        Assert.IsTrue(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest or HttpStatusCode.MethodNotAllowed,
            $"Unexpected status {response.StatusCode}");
        Assert.AreEqual(0, _factory.Foundry.DownloadCalls);
    }

    [TestMethod]
    public async Task TokenFromAnotherTenant_IsRejected()
    {
        var response = await _factory.CreateUserClient(UserA, tenantId: OtherTenant).GetAsync("/api/conversations");
        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task UnknownApiRoute_Returns404ProblemDetails_NotTheSpa()
    {
        var response = await _factory.CreateUserClient(UserA).GetAsync("/api/no-existe");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.AreEqual("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    // ===================== Lote 3: backend security (target behavior) =====================

    [TestMethod]
    [Ignore("Lote 3: /api/agent sin Instructions ni metadata interna")]
    public async Task AgentMetadata_DoesNotExposeInstructionsOrInternalMetadata()
    {
        var body = await _factory.CreateUserClient(UserA).GetStringAsync("/api/agent");

        Assert.IsFalse(body.Contains("instructions", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(body.Contains("\"internal\"", StringComparison.Ordinal));
    }

    [TestMethod]
    [Ignore("Lote 3: validación previa (400) sin crear conversación ni subir archivos")]
    public async Task InvalidAttachment_Returns400_WithoutSideEffects()
    {
        var (response, events) = await _factory.CreateUserClient(UserA).PostChatAsync(new
        {
            message = "mira",
            fileDataUris = new[] { new { dataUri = "data:text/html;base64,PGI+eDwvYj4=", fileName = "x.html", mimeType = "text/html" } }
        });

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.AreEqual(0, events.Count);
        Assert.AreEqual(0, _factory.Foundry.ConversationIds.Count);
    }

    [TestMethod]
    [Ignore("Lote 3: cabeceras de seguridad y CSP compatible con MSAL")]
    public async Task Responses_CarrySecurityHeaders()
    {
        var response = await _factory.CreateClient().GetAsync("/api/health");
        var csp = response.Headers.GetValues("Content-Security-Policy").Single();

        Assert.AreEqual("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        StringAssert.Contains(csp, "frame-ancestors 'none'");
        StringAssert.Contains(csp, "object-src 'none'");
        StringAssert.Contains(csp, "base-uri 'self'");
        StringAssert.Contains(csp, "img-src 'self' data: blob:");
        StringAssert.Contains(csp, "https://login.microsoftonline.com");
        Assert.IsTrue(response.Headers.Contains("Strict-Transport-Security"));
    }

    private static async Task<string> StartConversationAsync(HttpClient client)
    {
        var (response, events) = await client.PostChatAsync(new { message = "primer mensaje" });
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        return events[0].GetProperty("conversationId").GetString()!;
    }
}
