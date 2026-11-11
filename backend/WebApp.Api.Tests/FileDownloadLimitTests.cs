using System.Net;
using System.Text.Json;
using WebApp.Api.Infrastructure;
using WebApp.Api.Services;
using WebApp.Api.Tests.TestSupport;

namespace WebApp.Api.Tests;

/// <summary>
/// ChatLimits:MaxDownloadBytes on GET /api/files/{fileId}: files above the limit get 413 without
/// their content being loaded, and the ownership/association checks still run first (non-owners
/// get 404 and no size information).
/// </summary>
[TestClass]
public sealed class FileDownloadLimitTests
{
    private const string UserA = "aaaaaaaa-0000-4000-8000-00000000000a";
    private const string UserB = "bbbbbbbb-0000-4000-8000-00000000000b";
    private const long Limit = 2048;

    private ApiFactory _factory = null!;

    [TestInitialize]
    public void Initialize() =>
        _factory = new ApiFactory(new Dictionary<string, string?> { ["ChatLimits:MaxDownloadBytes"] = Limit.ToString() });

    [TestCleanup]
    public void Cleanup() => _factory.Dispose();

    // ---------- Endpoint (real pipeline, fake Foundry) ----------

    [TestMethod]
    [DataRow(Limit - 1, DisplayName = "below the limit")]
    [DataRow(Limit, DisplayName = "exactly the limit")]
    public async Task Owner_DownloadsFile_UpToTheLimit(long size)
    {
        _factory.Foundry.CitedFileSize = size;
        var client = _factory.CreateUserClient(UserA);
        var conversationId = await StartConversationAsync(client);

        var response = await client.GetAsync($"/api/files/{FakeFoundry.CitedFileId}?conversationId={conversationId}");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(size, (await response.Content.ReadAsByteArrayAsync()).LongLength);
    }

    [TestMethod]
    public async Task Owner_Gets413ProblemDetails_AboveTheLimit_WithoutLoadingTheContent()
    {
        _factory.Foundry.CitedFileSize = Limit + 1;
        var client = _factory.CreateUserClient(UserA);
        var conversationId = await StartConversationAsync(client);

        var response = await client.GetAsync($"/api/files/{FakeFoundry.CitedFileId}?conversationId={conversationId}");

        Assert.AreEqual(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.AreEqual("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.AreEqual(413, problem.RootElement.GetProperty("status").GetInt32());
        Assert.AreEqual(Limit, problem.RootElement.GetProperty("maxBytes").GetInt64());
        // The real file size is never disclosed.
        StringAssert.DoesNotMatch(problem.RootElement.ToString(), new System.Text.RegularExpressions.Regex((Limit + 1).ToString()));
        Assert.AreEqual(0, _factory.Foundry.ContentLoads, "the content of a file known to be too large must not be loaded");
    }

    [TestMethod]
    public async Task NonOwner_Gets404_NotTheSize_EvenForAnOversizedFile()
    {
        _factory.Foundry.CitedFileSize = Limit * 10;
        var conversationId = await StartConversationAsync(_factory.CreateUserClient(UserA));

        var response = await _factory.CreateUserClient(UserB)
            .GetAsync($"/api/files/{FakeFoundry.CitedFileId}?conversationId={conversationId}");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        StringAssert.DoesNotMatch(body, new System.Text.RegularExpressions.Regex("maxBytes|exceeds|Too Large", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
        Assert.AreEqual(0, _factory.Foundry.DownloadCalls, "the size check must run after ownership");
    }

    [TestMethod]
    [DataRow("informe.html")]
    [DataRow("grafico.svg")]
    [DataRow("script.js")]
    public async Task ActiveContent_IsStillServedAsOctetStream(string fileName)
    {
        _factory.Foundry.CitedFileName = fileName;
        var client = _factory.CreateUserClient(UserA);
        var conversationId = await StartConversationAsync(client);

        var response = await client.GetAsync($"/api/files/{FakeFoundry.CitedFileId}?conversationId={conversationId}");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("application/octet-stream", response.Content.Headers.ContentType?.MediaType);
    }

    // ---------- DownloadLimits (used by the real AgentFrameworkService) ----------

    [TestMethod]
    [DataRow(null, DisplayName = "unknown size")]
    [DataRow(Limit - 1, DisplayName = "below")]
    [DataRow(Limit, DisplayName = "equal")]
    public void EnsureWithinLimit_Accepts(long? size) => DownloadLimits.EnsureWithinLimit(size, Limit);

    [TestMethod]
    public void EnsureWithinLimit_RejectsAboveTheLimit() =>
        Assert.ThrowsExactly<FileTooLargeException>(() => DownloadLimits.EnsureWithinLimit(Limit + 1, Limit));

    [TestMethod]
    [DataRow(Limit - 1)]
    [DataRow(Limit)]
    public async Task ReadBounded_ReadsStreamsThatFit(long size)
    {
        using var source = new CountingStream(size);

        var bytes = await DownloadLimits.ReadBoundedAsync(source, Limit, CancellationToken.None);

        Assert.AreEqual(size, bytes.LongLength);
    }

    [TestMethod]
    public async Task ReadBounded_StopsOneBytePastTheLimit_WhenTheSizeIsUnknown()
    {
        using var source = new CountingStream(Limit * 100); // much larger than the limit

        await Assert.ThrowsExactlyAsync<FileTooLargeException>(
            () => DownloadLimits.ReadBoundedAsync(source, Limit, CancellationToken.None));
        Assert.IsTrue(source.BytesRead <= Limit + 1, $"read {source.BytesRead} bytes, expected at most {Limit + 1}");
    }

    private static async Task<string> StartConversationAsync(HttpClient client)
    {
        var (response, events) = await client.PostChatAsync(new { message = "primer mensaje" });
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        return events[0].GetProperty("conversationId").GetString()!;
    }

    /// <summary>Non-seekable stream of <c>length</c> zero bytes that counts what was read.</summary>
    private sealed class CountingStream(long length) : Stream
    {
        public long BytesRead { get; private set; }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => BytesRead; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var n = (int)Math.Min(count, length - BytesRead);
            Array.Clear(buffer, offset, n);
            BytesRead += n;
            return n;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
