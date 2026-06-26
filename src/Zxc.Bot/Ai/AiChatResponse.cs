using System.Text.Json.Serialization;

namespace Zxc.Bot.Ai;

public sealed record AiChatResponse(
    [property: JsonPropertyName("choices")] IReadOnlyList<AiChatChoice> Choices);

public sealed record AiChatChoice(
    [property: JsonPropertyName("message")] AiChatResponseMessage Message,
    [property: JsonPropertyName("finish_reason")] string? FinishReason);

public sealed record AiChatResponseMessage(
    [property: JsonPropertyName("role")] string? Role,
    [property: JsonPropertyName("content")] string? Content,
    [property: JsonPropertyName("reasoning_content")] string? ReasoningContent);
