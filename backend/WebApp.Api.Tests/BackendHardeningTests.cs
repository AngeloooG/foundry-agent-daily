using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Net.Http.Json;
using WebApp.Api.Infrastructure;
using WebApp.Api.Security;
using WebApp.Api.Tests.TestSupport;

namespace WebApp.Api.Tests;

/// <summary>Lote 3: limits, rate limiting, error sanitization, CSP and headers.</summary>
[TestClass]
public sealed class BackendHardeningTests
{
    private const string UserA = "aaaaaaaa-0000-4000-8000-00000000000a";
    private const string UserB = "bbbbbbbb-0000-4000-8000-00000000000b";

    [TestMethod]
    public async Task TooLongMessage_Returns400_BeforeCallingFoundry()
    {
        using var factory = new ApiFactory(new Dictionary<string, string?> { ["ChatLimits:MaxMessageLength"] = "10" });

        var (response, events) = await factory.CreateUserClient(UserA).PostChatAsync(new { message = new string('x', 11) });

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.AreEqual(0, events.Count);
        Assert.AreEqual(0, factory.Foundry.ConversationIds.Count);
        Assert.AreEqual(0, factory.Foundry.StreamCalls);
    }

    [TestMethod]
    public async Task ValidationErrors_ExplainWhatToFix()
    {
        using var factory = new ApiFactory();

        var (response, _) = await factory.CreateUserClient(UserA).PostChatAsync(new
        {
            message = "mira",
            imageDataUris = new[] { "data:image/png;base64,bm90IGEgcG5n" } // "not a png"
        });

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        StringAssert.Contains(await response.Content.ReadAsStringAsync(), "Image 1");
    }

    [TestMethod]
    public async Task ChatRateLimit_IsPerUser_AndReturns429WithRetryAfter()
    {
        using var factory = new ApiFactory(new Dictionary<string, string?> { ["ChatLimits:ChatRequestsPerMinute"] = "2" });
        var client = factory.CreateUserClient(UserA);

        await client.PostChatAsync(new { message = "1" });
        await client.PostChatAsync(new { message = "2" });
        var (third, _) = await client.PostChatAsync(new { message = "3" });
        var (otherUser, _) = await factory.CreateUserClient(UserB).PostChatAsync(new { message = "1" });

        Assert.AreEqual(HttpStatusCode.TooManyRequests, third.StatusCode);
        Assert.IsTrue(third.Headers.Contains("Retry-After"));
        Assert.AreEqual(HttpStatusCode.OK, otherUser.StatusCode);
    }

    [TestMethod]
    public async Task HealthChecks_AreNotRateLimited()
    {
        using var factory = new ApiFactory(new Dictionary<string, string?> { ["ChatLimits:ApiRequestsPerMinute"] = "1" });
        var client = factory.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            Assert.AreEqual(HttpStatusCode.OK, (await client.GetAsync("/api/health")).StatusCode);
        }
    }

    [TestMethod]
    public async Task OversizedBody_IsRejectedByKestrel_BeforeAnySideEffect()
    {
        // Body limits are enforced by the real server (TestServer ignores them), so run on Kestrel.
        using var factory = new ApiFactory(new Dictionary<string, string?>
        {
            ["ChatLimits:MaxRequestBodyBytes"] = "4096",
            ["ChatLimits:MaxTotalAttachmentBytes"] = "1024"
        });
        factory.UseKestrel(0);
        factory.StartServer();

        var (response, _) = await factory.CreateUserClient(UserA).PostChatAsync(new { message = new string('x', 8_000) });

        Assert.AreEqual(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.AreEqual(0, factory.Foundry.ConversationIds.Count);
    }

    [TestMethod]
    public async Task StreamFailureAfterStart_SendsSanitizedErrorEvent()
    {
        using var factory = new ApiFactory();
        factory.Foundry.FailAfterFirstChunk = new InvalidOperationException("internal detail: host=secret-host.internal");

        var (_, events) = await factory.CreateUserClient(UserA).PostChatAsync(new { message = "hola" });

        var types = events.Types();
        CollectionAssert.Contains(types, "error");
        CollectionAssert.DoesNotContain(types, "done");
        var message = events.Single(e => e.GetProperty("type").GetString() == "error").GetProperty("message").GetString()!;
        Assert.IsFalse(message.Contains("secret-host"), "Production errors must not leak internal details.");
    }

    [TestMethod]
    public async Task AgentMetadata_KeepsPublicFieldsOnly()
    {
        using var factory = new ApiFactory();

        var json = await factory.CreateUserClient(UserA).GetFromJsonAsync<System.Text.Json.JsonElement>("/api/agent");

        Assert.AreEqual("daily.svg", json.GetProperty("metadata").GetProperty("logo").GetString());
        Assert.IsFalse(json.GetProperty("metadata").TryGetProperty("internal", out _));
        Assert.IsFalse(json.TryGetProperty("instructions", out _));
    }

    [TestMethod]
    public async Task SilentRedirectPage_IsDisabledByDefault()
    {
        using var factory = new ApiFactory();

        var response = await factory.CreateClient().GetAsync(SecurityHeadersOptions.SilentRedirectPath);

        StringAssert.Contains(response.Headers.GetValues("Content-Security-Policy").Single(), "frame-ancestors 'none'");
        Assert.AreEqual("DENY", response.Headers.GetValues("X-Frame-Options").Single());
    }

    [TestMethod]
    public async Task SilentRedirectPage_WhenEnabled_AllowsSameOriginFramingOnlyOnThatPath()
    {
        using var factory = new ApiFactory(new Dictionary<string, string?> { ["SecurityHeaders:SilentRedirectPageEnabled"] = "true" });
        var client = factory.CreateClient();

        var redirectPage = await client.GetAsync(SecurityHeadersOptions.SilentRedirectPath);
        var otherPath = await client.GetAsync("/api/health");

        StringAssert.Contains(redirectPage.Headers.GetValues("Content-Security-Policy").Single(), "frame-ancestors 'self'");
        Assert.AreEqual("SAMEORIGIN", redirectPage.Headers.GetValues("X-Frame-Options").Single());
        StringAssert.Contains(otherPath.Headers.GetValues("Content-Security-Policy").Single(), "frame-ancestors 'none'");
    }

    [TestMethod]
    public async Task Csp_IncludesConfiguredApplicationInsightsEndpoints()
    {
        // Fake, key-less connection string: only the endpoint hosts matter.
        using var factory = new ApiFactory(new Dictionary<string, string?>
        {
            ["APPLICATIONINSIGHTS_FRONTEND_CONNECTION_STRING"] =
                "IngestionEndpoint=https://centralus-2.in.applicationinsights.azure.com/;LiveEndpoint=https://centralus.livediagnostics.monitor.azure.com/"
        });

        var csp = (await factory.CreateClient().GetAsync("/api/health")).Headers.GetValues("Content-Security-Policy").Single();

        StringAssert.Contains(csp, "https://centralus-2.in.applicationinsights.azure.com");
        StringAssert.Contains(csp, "https://centralus.livediagnostics.monitor.azure.com");
    }
}

[TestClass]
public sealed class ContentSecurityPolicyBuilderTests
{
    [TestMethod]
    public void Build_HasRequiredDirectives()
    {
        var csp = ContentSecurityPolicyBuilder.Build([], allowSelfFraming: false);

        foreach (var directive in new[]
        {
            "default-src 'self'", "base-uri 'self'", "object-src 'none'", "frame-ancestors 'none'",
            "script-src 'self'", "img-src 'self' data: blob:", "frame-src https://login.microsoftonline.com",
            "connect-src 'self' https://login.microsoftonline.com"
        })
        {
            StringAssert.Contains(csp, directive);
        }
        Assert.IsFalse(csp.Contains("script-src 'self' 'unsafe-inline'"), "Inline scripts must stay blocked.");
    }

    [TestMethod]
    public void AppInsightsOrigins_ReadOnlyHttpsEndpointHosts()
    {
        var origins = ContentSecurityPolicyBuilder.AppInsightsOrigins(
            "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://region-1.in.applicationinsights.azure.com/;LiveEndpoint=http://insecure.example/;ApplicationId=x").ToList();

        CollectionAssert.AreEqual(new[] { "https://region-1.in.applicationinsights.azure.com" }, origins);
        Assert.AreEqual(0, ContentSecurityPolicyBuilder.AppInsightsOrigins(null).Count());
    }
}

[TestClass]
public sealed class ApiExceptionHandlerTests
{
    private sealed class StatusResponse(int status) : PipelineResponse
    {
        public override int Status => status;
        public override string ReasonPhrase => string.Empty;
        public override Stream? ContentStream { get; set; }
        public override BinaryData Content => BinaryData.Empty;
        protected override PipelineResponseHeaders HeadersCore => throw new NotSupportedException();
        public override BinaryData BufferContent(CancellationToken cancellationToken = default) => BinaryData.Empty;
        public override ValueTask<BinaryData> BufferContentAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(BinaryData.Empty);
        public override void Dispose() { }
    }

    [TestMethod]
    [DataRow(404, 404)]
    [DataRow(401, 502, DisplayName = "Foundry 401 is a server problem, not an expired user session")]
    [DataRow(403, 502)]
    [DataRow(429, 503)]
    [DataRow(500, 502)]
    public void FoundryErrors_MapToGatewayStatuses(int upstream, int expected)
    {
        Assert.AreEqual(expected, ApiExceptionHandler.MapStatusCode(new ClientResultException(new StatusResponse(upstream))));
    }

    [TestMethod]
    public void OtherExceptions_Map()
    {
        Assert.AreEqual(400, ApiExceptionHandler.MapStatusCode(new RequestValidationException("bad")));
        Assert.AreEqual(401, ApiExceptionHandler.MapStatusCode(new UnauthorizedAccessException()));
        Assert.AreEqual(404, ApiExceptionHandler.MapStatusCode(new HttpRequestException("x", null, HttpStatusCode.NotFound)));
        Assert.AreEqual(500, ApiExceptionHandler.MapStatusCode(new InvalidOperationException()));
    }

    [TestMethod]
    public void ValidationDetailsAreShown_InternalDetailsAreNot()
    {
        var validation = ApiExceptionHandler.CreateProblemDetails(new RequestValidationException("File 1 (a.pdf): too big."), 400, isDevelopment: false);
        var internalError = ApiExceptionHandler.CreateProblemDetails(new InvalidOperationException("connection string=..."), 500, isDevelopment: false);

        Assert.AreEqual("File 1 (a.pdf): too big.", validation.Detail);
        Assert.IsFalse(internalError.Detail!.Contains("connection string"));
        Assert.IsFalse(internalError.Extensions.ContainsKey("stackTrace"));
    }
}
