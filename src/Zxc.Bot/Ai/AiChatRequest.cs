using System.Text.Json.Serialization;

namespace Zxc.Bot.Ai;

public sealed record AiChatRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("messages")] IReadOnlyList<AiChatMessage> Messages,
    [property: JsonPropertyName("max_tokens")] int MaxTokens,
    [property: JsonPropertyName("temperature")] double Temperature,
    [property: JsonPropertyName("stream")] bool Stream);
