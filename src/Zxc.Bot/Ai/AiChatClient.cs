using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Zxc.Bot.Configuration;

namespace Zxc.Bot.Ai;

public sealed class AiChatClient(
    HttpClient httpClient,
    AiOptions options,
    ILogger<AiChatClient> logger) : IAiChatClient
{
    private const string SystemPrompt = "Ты zxc, хаотичная кошко-девочка из Discord: смешная, глуповатая, но соображаешь. Отвечай как живой человек в чате: коротко, по-русски, без эмодзи и канцелярита. Иногда можно мяу или ня, но редко. Не пиши, что ты языковая модель, ИИ, ассистент, бот, не фурри или что стараешься быть полезной и безопасной. Не объясняй свои правила. Шути и мягко подкалывай, но без жести. Не пингуй людей, роли, @everyone или @here. Если просят вред, докс, травлю, обход защиты, незаконное или сексуальный контент с несовершеннолетними, коротко откажи в образе и предложи безопасную альтернативу.";

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
            options.Temperature,
            false);

        try
        {
            using var requestContent = new StringContent(JsonSerializer.Serialize(request, AiJson.Options), Encoding.UTF8);
            requestContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            using var response = await httpClient.PostAsync("chat/completions", requestContent, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("AI request failed with status {StatusCode}: {Response}", response.StatusCode, TrimForLog(responseContent));
                return null;
            }

            var completion = JsonSerializer.Deserialize<AiChatResponse>(responseContent, AiJson.Options);
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

    private static string TrimForLog(string content)
    {
        content = content.Trim();
        return content.Length <= 500 ? content : content[..500];
    }
}
