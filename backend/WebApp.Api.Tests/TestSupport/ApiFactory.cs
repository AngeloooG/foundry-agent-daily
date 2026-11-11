using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WebApp.Api.Security;
using WebApp.Api.Services;

namespace WebApp.Api.Tests.TestSupport;

/// <summary>
/// Hosts the real API pipeline in memory with Foundry replaced by <see cref="FakeFoundry"/> and
/// Entra replaced by <see cref="TestAuthHandler"/>. Runs as "Production" so production-only
/// behavior (error sanitization, headers, CORS) is exercised. No Azure calls are made.
/// </summary>
public sealed class ApiFactory(IReadOnlyDictionary<string, string?>? settings = null) : WebApplicationFactory<Program>
{
    public FakeFoundry Foundry { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting("AI_AGENT_ENDPOINT", "https://fake.services.ai.azure.com/api/projects/test");
        builder.UseSetting("AI_AGENT_ID", "Daily-Enhanced");
        builder.UseSetting("ENTRA_SPA_CLIENT_ID", "22222222-2222-2222-2222-222222222222");
        builder.UseSetting("ENTRA_TENANT_ID", TestAuthHandler.HomeTenantId);
        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultScheme = TestAuthHandler.SchemeName;
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                options.DefaultForbidScheme = TestAuthHandler.SchemeName;
            });

            services.RemoveAll<AgentFrameworkService>();
            services.RemoveAll<IAgentFrameworkService>();
            services.RemoveAll<IConversationOwnershipStore>();
            services.AddSingleton<IAgentFrameworkService>(Foundry);
            services.AddSingleton<IConversationOwnershipStore>(Foundry);
        });
    }

    /// <summary>Client authenticated as the given object id (pseudonymous test ids only).</summary>
    public HttpClient CreateUserClient(string objectId, string? tenantId = null, string? scopes = null)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, objectId);
        if (tenantId is not null) client.DefaultRequestHeaders.Add(TestAuthHandler.TenantHeader, tenantId);
        if (scopes is not null) client.DefaultRequestHeaders.Add(TestAuthHandler.ScopesHeader, scopes);
        return client;
    }
}

public static class SseTestExtensions
{
    /// <summary>Posts to /api/chat/stream and parses the SSE frames (if the response is a stream).</summary>
    public static async Task<(HttpResponseMessage Response, List<JsonElement> Events)> PostChatAsync(this HttpClient client, object body)
    {
        var response = await client.PostAsJsonAsync("/api/chat/stream", body);
        var events = new List<JsonElement>();
        if (response.Content.Headers.ContentType?.MediaType == "text/event-stream")
        {
            foreach (var frame in (await response.Content.ReadAsStringAsync()).Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
            {
                if (frame.StartsWith("data: ", StringComparison.Ordinal))
                {
                    events.Add(JsonDocument.Parse(frame["data: ".Length..]).RootElement.Clone());
                }
            }
        }
        return (response, events);
    }

    public static List<string?> Types(this IEnumerable<JsonElement> events) =>
        events.Select(e => e.GetProperty("type").GetString()).ToList();
}
