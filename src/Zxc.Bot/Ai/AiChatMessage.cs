using System.Text.Json.Serialization;

namespace Zxc.Bot.Ai;

public sealed record AiChatMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);
