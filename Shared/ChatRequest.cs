namespace Shared;

public class ChatRequest
{
    public ChatMessage[] History { get; set; } = [];
    public ChatOverrides Overrides { get; set; } = new();
    
    public string? LastUserQuestion => History?.Last(m => m.Role == "user")?.Content;
}