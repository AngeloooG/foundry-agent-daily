using System.Security.Claims;
using WebApp.Api.Security;

namespace WebApp.Api.Tests;

[TestClass]
public sealed class UserIdentityTests
{
    private const string Tenant = "48235e11-59f7-416c-9d73-f5d23b9e3ad2";

    private static ClaimsPrincipal User(params (string Type, string Value)[] claims) =>
        new(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)), "Test"));

    [TestMethod]
    public void OwnerKey_IsLowercaseHexSha256OfTidColonOid()
    {
        var expected = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData("t:o"u8));

        var actual = UserIdentity.ComputeOwnerKey("t", "o");

        Assert.AreEqual(expected, actual);
        Assert.AreEqual(64, actual.Length);
        Assert.AreEqual(actual, actual.ToLowerInvariant());
    }

    [TestMethod]
    public void OwnerKey_FromClaims_AcceptsShortAndMappedClaimTypes()
    {
        var shortForm = UserIdentity.GetOwnerKey(User(("oid", "o1"), ("tid", Tenant)));
        var longForm = UserIdentity.GetOwnerKey(User(
            ("http://schemas.microsoft.com/identity/claims/objectidentifier", "o1"),
            ("http://schemas.microsoft.com/identity/claims/tenantid", Tenant)));

        Assert.AreEqual(UserIdentity.ComputeOwnerKey(Tenant, "o1"), shortForm);
        Assert.AreEqual(shortForm, longForm);
    }

    [TestMethod]
    public void OwnerKey_IsNullWithoutOidOrAuthentication()
    {
        Assert.IsNull(UserIdentity.GetOwnerKey(User(("tid", Tenant))));
        Assert.IsNull(UserIdentity.GetOwnerKey(new ClaimsPrincipal(new ClaimsIdentity())));
    }

    [TestMethod]
    public void Tenant_MustMatchConfiguredTenant_AndFailsClosed()
    {
        Assert.IsTrue(UserIdentity.IsAllowedTenant(User(("tid", Tenant)), Tenant));
        Assert.IsFalse(UserIdentity.IsAllowedTenant(User(("tid", "other")), Tenant));
        Assert.IsFalse(UserIdentity.IsAllowedTenant(User(("tid", Tenant)), null), "No configured tenant must deny everyone.");
    }
}

[TestClass]
public sealed class ConversationMetadataTests
{
    [TestMethod]
    public void NewConversation_HasOwnerAndSingleLineTruncatedTitle()
    {
        var metadata = ConversationMetadata.ForNewConversation("owner-key", "Línea uno\nlínea dos " + new string('x', 80));

        Assert.AreEqual("owner-key", metadata[ConversationMetadata.OwnerKey]);
        Assert.IsFalse(metadata[ConversationMetadata.TitleKey].Contains('\n'));
        Assert.AreEqual(ConversationMetadata.MaxTitleLength + 3, metadata[ConversationMetadata.TitleKey].Length);
    }

    [TestMethod]
    public void WithFiles_KeepsOwnerAndTitle_AndRecordsFiles()
    {
        var original = ConversationMetadata.ForNewConversation("owner-key", "hola");

        var updated = ConversationMetadata.WithFiles(original, [new AuthorizedFile("file_1", null), new AuthorizedFile("cfile_2", "cntr_1")]);

        Assert.AreEqual("owner-key", updated[ConversationMetadata.OwnerKey]);
        Assert.AreEqual("hola", updated[ConversationMetadata.TitleKey]);
        Assert.IsTrue(ConversationMetadata.ContainsFile(updated, "file_1", null));
        Assert.IsTrue(ConversationMetadata.ContainsFile(updated, "cfile_2", "cntr_1"));
        Assert.IsFalse(ConversationMetadata.ContainsFile(updated, "cfile_2", null), "Container files require their container id.");
        Assert.IsFalse(ConversationMetadata.ContainsFile(updated, "file_3", null));
    }

    [TestMethod]
    public void WithFiles_DoesNotDuplicate()
    {
        var once = ConversationMetadata.WithFiles(new Dictionary<string, string>(), [new AuthorizedFile("file_1", null)]);
        var twice = ConversationMetadata.WithFiles(once, [new AuthorizedFile("file_1", null)]);

        Assert.AreEqual(1, ConversationMetadata.ReadFiles(twice).Count);
    }

    [TestMethod]
    public void WithFiles_RespectsFoundryMetadataLimits_DroppingOldest()
    {
        var files = Enumerable.Range(0, 400).Select(i => new AuthorizedFile($"assistant-file-{i:D4}-abcdefghij", null)).ToList();

        var updated = ConversationMetadata.WithFiles(ConversationMetadata.ForNewConversation("owner-key", "t"), files);

        Assert.IsTrue(updated.Count <= 16, "Foundry allows at most 16 metadata keys.");
        Assert.IsTrue(updated.Values.All(v => v.Length <= ConversationMetadata.MaxValueLength));
        Assert.IsTrue(ConversationMetadata.ContainsFile(updated, files[^1].FileId, null), "Newest file must be kept.");
        Assert.IsFalse(ConversationMetadata.ContainsFile(updated, files[0].FileId, null), "Oldest file is dropped when full.");
    }

    [TestMethod]
    public void WithFiles_IgnoresIdsThatWouldBreakTheEncoding()
    {
        var updated = ConversationMetadata.WithFiles(new Dictionary<string, string>(), [new AuthorizedFile("a,b", null)]);
        Assert.AreEqual(0, ConversationMetadata.ReadFiles(updated).Count);
    }

    [TestMethod]
    [DataRow("conv_123", true)]
    [DataRow("conv-ABC", true)]
    [DataRow("", false)]
    [DataRow("../conv", false)]
    [DataRow("conv 1", false)]
    [DataRow("conv?x=1", false)]
    public void ConversationIdFormat(string id, bool valid)
    {
        Assert.AreEqual(valid, ConversationMetadata.IsValidConversationId(id));
    }
}
