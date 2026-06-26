using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Zxc.Bot.Ai;
using Zxc.Bot.Configuration;

namespace Zxc.Bot.Discord;

public sealed class AiMentionResponder(
    DiscordSocketClient client,
    IAiChatClient aiChatClient,
    AiOptions options,
    ILogger<AiMentionResponder> logger)
{
    public async Task HandleAsync(SocketMessage message)
    {
        if (!options.Enabled)
            return;

        if (message.Author.IsBot)
            return;

        if (client.CurrentUser == null)
            return;

        if (message is not SocketUserMessage userMessage)
            return;

        if (!userMessage.MentionedUsers.Any(user => user.Id == client.CurrentUser.Id))
            return;

        var prompt = BuildPrompt(userMessage.Content, client.CurrentUser.Id);
        if (string.IsNullOrWhiteSpace(prompt))
            prompt = "Скажи что-нибудь короткое и милое.";

        if (prompt.Length > options.MaxPromptChars)
            prompt = prompt[..options.MaxPromptChars];

        try
        {
            using var typing = userMessage.Channel.EnterTypingState();
            using var timeout = new CancellationTokenSource(options.Timeout);
            var reply = await aiChatClient.CreateReplyAsync(prompt, timeout.Token) ?? "Мяу...";

            await userMessage.Channel.SendMessageAsync(
                reply,
                allowedMentions: AllowedMentions.None,
                messageReference: new MessageReference(userMessage.Id));
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failed to answer AI mention.");
        }
    }

    private static string BuildPrompt(string content, ulong botUserId)
    {
        return content
            .Replace($"<@{botUserId}>", string.Empty, StringComparison.Ordinal)
            .Replace($"<@!{botUserId}>", string.Empty, StringComparison.Ordinal)
            .Trim();
    }
}
