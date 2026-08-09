using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebApp.Api.Models;
using WebApp.Api.Services;

namespace WebApp.Api.Tests;

[TestClass]
public class ConversationStoreTests
{
    [TestMethod]
    public void CreateConversation_StoresTitleAndReturnsConversationId()
    {
        var store = new ConversationStore();

        var conversationId = store.CreateConversation("Hello from tests");
        var conversations = store.ListConversations();

        Assert.AreEqual(1, conversations.Count);
        Assert.AreEqual(conversationId, conversations[0].Id);
        Assert.AreEqual("Hello from tests", conversations[0].Title);
    }

    [TestMethod]
    public void AddMessage_AndGetMessages_RoundTripMessages()
    {
        var store = new ConversationStore();
        var conversationId = store.CreateConversation("Round trip");

        store.AddMessage(conversationId, "user", "first message");
        store.AddMessage(conversationId, "assistant", "reply message");

        var messages = store.GetMessages(conversationId);

        Assert.AreEqual(2, messages.Count);
        Assert.AreEqual("user", messages[0].Role);
        Assert.AreEqual("first message", messages[0].Content);
        Assert.AreEqual("assistant", messages[1].Role);
        Assert.AreEqual("reply message", messages[1].Content);
    }

    [TestMethod]
    public void EnsureConversation_CreatesEntryForExistingConversationId()
    {
        var store = new ConversationStore();

        store.EnsureConversation("existing-conversation", "Persisted title");

        var conversations = store.ListConversations();
        Assert.AreEqual(1, conversations.Count);
        Assert.AreEqual("existing-conversation", conversations[0].Id);
        Assert.AreEqual("Persisted title", conversations[0].Title);
    }
}
