using System.Text.Json.Serialization;

namespace Shared;

public record ChatMessage(
    [property:JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content)
{
    public bool IsUser => Role == "user";
}