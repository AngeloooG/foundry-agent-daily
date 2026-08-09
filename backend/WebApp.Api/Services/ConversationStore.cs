using System.Collections.Concurrent;
using WebApp.Api.Models;

namespace WebApp.Api.Services;

public sealed class ConversationStore
{
    private readonly ConcurrentDictionary<string, ConversationRecord> _conversations = new();

    public string CreateConversation(string? firstMessage = null)
    {
        var conversationId = Guid.NewGuid().ToString("N");
        EnsureConversation(conversationId, firstMessage);
        return conversationId;
    }

    public void EnsureConversation(string conversationId, string? firstMessage = null)
    {
        var title = firstMessage is { Length: > 0 } ? firstMessage.Trim() : null;

        _conversations.AddOrUpdate(
            conversationId,
            _ => new ConversationRecord
            {
                Id = conversationId,
                Title = title,
                CreatedAt = DateTimeOffset.UtcNow,
                Messages = new List<ConversationMessageInfo>()
            },
            (_, existing) =>
            {
                if (string.IsNullOrWhiteSpace(existing.Title) && !string.IsNullOrWhiteSpace(title))
                {
                    existing.Title = title;
                }

                return existing;
            });
    }

    public List<ConversationSummary> ListConversations()
    {
        return _conversations.Values
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ConversationSummary
            {
                Id = x.Id,
                Title = x.Title,
                CreatedAt = x.CreatedAt.ToUnixTimeSeconds()
            })
            .ToList();
    }

    public void AddMessage(string conversationId, string role, string content)
    {
        if (!_conversations.TryGetValue(conversationId, out var conversation))
        {
            conversation = new ConversationRecord
            {
                Id = conversationId,
                CreatedAt = DateTimeOffset.UtcNow,
                Messages = new List<ConversationMessageInfo>()
            };
            _conversations[conversationId] = conversation;
        }

        conversation.Messages.Add(new ConversationMessageInfo
        {
            Role = role,
            Content = content
        });
    }

    public List<ConversationMessageInfo> GetMessages(string conversationId)
    {
        if (!_conversations.TryGetValue(conversationId, out var conversation))
        {
            return new List<ConversationMessageInfo>();
        }

        return conversation.Messages.ToList();
    }

    private sealed class ConversationRecord
    {
        public required string Id { get; init; }
        public string? Title { get; set; }
        public DateTimeOffset CreatedAt { get; init; }
        public List<ConversationMessageInfo> Messages { get; init; } = new();
    }
}
