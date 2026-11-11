using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WebApp.Api.Infrastructure;
using WebApp.Api.Models;
using WebApp.Api.Options;
using WebApp.Api.Services;
using WebApp.Api.Tests.TestSupport;

namespace WebApp.Api.Tests;

/// <summary>
/// Default limits: MaxInlineTextChars lowered from 200,000 to 50,000 (still configurable). The other
/// limits (images, PDF/files, counts, HTTP body) must not change.
/// </summary>
[TestClass]
public sealed class ChatLimitsDefaultsTests
{
    [TestMethod]
    public void InlineTextDefault_Is50000()
    {
        Assert.AreEqual(50_000, new ChatLimitsOptions().MaxInlineTextChars);
    }

    [TestMethod]
    public void AppSettings_UseTheSameDefault_AndOtherLimitsAreUnchanged()
    {
        using var factory = new ApiFactory();
        var limits = factory.Services.GetRequiredService<IOptions<ChatLimitsOptions>>().Value;

        Assert.AreEqual(50_000, limits.MaxInlineTextChars);
        Assert.AreEqual(16_000, limits.MaxMessageLength);
        Assert.AreEqual(5, limits.MaxImageCount);
        Assert.AreEqual(5L * 1024 * 1024, limits.MaxImageBytes);
        Assert.AreEqual(10, limits.MaxFileCount);
        Assert.AreEqual(20L * 1024 * 1024, limits.MaxFileBytes);
        Assert.AreEqual(20L * 1024 * 1024, limits.MaxTotalAttachmentBytes);
        Assert.AreEqual(30_000_000L, limits.MaxRequestBodyBytes);
        Assert.AreEqual(50L * 1024 * 1024, limits.MaxDownloadBytes);
    }

    [TestMethod]
    public void InlineTextLimit_IsStillConfigurable()
    {
        using var factory = new ApiFactory(new Dictionary<string, string?> { ["ChatLimits:MaxInlineTextChars"] = "1234" });
        var limits = factory.Services.GetRequiredService<IOptions<ChatLimitsOptions>>().Value;

        Assert.AreEqual(1234, limits.MaxInlineTextChars);
    }

    [TestMethod]
    [DataRow(50_000, true, DisplayName = "exactly 50,000 characters")]
    [DataRow(50_001, false, DisplayName = "50,001 characters")]
    public void DefaultLimit_AppliesToInlinedText(int characters, bool accepted)
    {
        var text = new string('a', characters);
        var dataUri = "data:text/plain;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
        var files = new List<FileAttachment> { new() { DataUri = dataUri, FileName = "notas.txt", MimeType = "text/plain" } };

        if (accepted)
        {
            var prepared = AttachmentProcessor.Prepare(null, files, new ChatLimitsOptions());
            Assert.AreEqual(1, prepared.InlineTexts.Count);
        }
        else
        {
            Assert.ThrowsExactly<RequestValidationException>(() => AttachmentProcessor.Prepare(null, files, new ChatLimitsOptions()));
        }
    }
}
