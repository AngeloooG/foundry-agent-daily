namespace WebApp.Api.Models;

/// <summary>
/// Public agent metadata returned to the SPA. Visible to every signed-in user, so it never carries
/// the agent instructions (system prompt) and only allowlisted metadata keys.
/// </summary>
public record AgentMetadataResponse
{
    public required string Id { get; init; }
    public required string Object { get; init; } = "agent";
    public required long CreatedAt { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string Model { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
    
    /// <summary>
    /// Starter prompts to display as suggestions in the chat interface.
    /// Populated from agent metadata key "starterPrompts" (newline-separated text).
    /// Configure in Microsoft Foundry portal under agent Configuration > Starter prompts.
    /// </summary>
    public List<string>? StarterPrompts { get; init; }
}
