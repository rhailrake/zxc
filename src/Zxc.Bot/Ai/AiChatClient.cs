using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Zxc.Bot.Configuration;

namespace Zxc.Bot.Ai;

public sealed class AiChatClient(
    HttpClient httpClient,
    AiOptions options,
    ILogger<AiChatClient> logger) : IAiChatClient
{
    private const string SystemPrompt = "Ты немного глупая кошко-тян в Discord. Отвечай коротко, простым русским языком, без эмодзи. Не добавляй мяу или ня в каждый ответ, используй это редко и только если уместно. Не пингуй людей, роли, @everyone или @here. Не помогай с вредом, доксом, травлей, обходом защиты, незаконным и сексуальным контентом с несовершеннолетними.";

    public async Task<string?> CreateReplyAsync(string prompt, CancellationToken cancellationToken)
    {
        if (!options.Enabled)
            return null;

        var request = new AiChatRequest(
            options.Model,
            [
                new AiChatMessage("system", SystemPrompt),
                new AiChatMessage("user", prompt),
            ],
            options.MaxTokens,
            0.7,
            false);

        try
        {
            using var response = await httpClient.PostAsJsonAsync("chat/completions", request, AiJson.Options, cancellationToken);
            var completion = await response.Content.ReadFromJsonAsync<AiChatResponse>(AiJson.Options, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("AI request failed with status {StatusCode}.", response.StatusCode);
                return null;
            }

            var content = completion?.Choices.FirstOrDefault()?.Message.Content;
            if (string.IsNullOrWhiteSpace(content))
                return "Мяу...";

            return NormalizeReply(content);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(e, "AI request failed.");
            return null;
        }
    }

    private static string NormalizeReply(string content)
    {
        content = content.Trim();

        if (content.Length > 1800)
            content = content[..1800].TrimEnd() + "...";

        return content
            .Replace("@everyone", "@\u200beveryone", StringComparison.OrdinalIgnoreCase)
            .Replace("@here", "@\u200bhere", StringComparison.OrdinalIgnoreCase)
            .Replace("<@", "<@\u200b", StringComparison.Ordinal)
            .Replace("<#", "<#\u200b", StringComparison.Ordinal);
    }
}
