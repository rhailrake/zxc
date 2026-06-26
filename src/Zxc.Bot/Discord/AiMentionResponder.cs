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
        logger.LogInformation(
            "AI message event. MessageId: {MessageId}. AuthorId: {AuthorId}. AuthorIsBot: {AuthorIsBot}. ChannelId: {ChannelId}. MessageType: {MessageType}. ContentLength: {ContentLength}.",
            message.Id,
            message.Author.Id,
            message.Author.IsBot,
            message.Channel.Id,
            message.GetType().Name,
            message.Content?.Length ?? 0);

        if (!options.Enabled)
        {
            logger.LogInformation("AI message ignored because AI is disabled. MessageId: {MessageId}.", message.Id);
            return;
        }

        if (message.Author.IsBot)
            return;

        if (client.CurrentUser == null)
        {
            logger.LogInformation("AI message ignored because current user is not ready. MessageId: {MessageId}.", message.Id);
            return;
        }

        if (message is not SocketUserMessage userMessage)
        {
            logger.LogInformation("AI message ignored because it is not a user message. MessageId: {MessageId}.", message.Id);
            return;
        }

        var mentionedUserIds = string.Join(',', userMessage.MentionedUsers.Select(user => user.Id));
        if (string.IsNullOrWhiteSpace(mentionedUserIds))
            mentionedUserIds = "<none>";

        var botMentionedByUsers = userMessage.MentionedUsers.Any(user => user.Id == client.CurrentUser.Id);
        var botMentionedByRawContent = ContainsBotMention(userMessage.Content, client.CurrentUser.Id);
        var botMentioned = botMentionedByUsers || botMentionedByRawContent;
        logger.LogInformation(
            "AI mention check. MessageId: {MessageId}. BotUserId: {BotUserId}. MentionedUsers: {MentionedUsers}. RawBotMention: {RawBotMention}. BotMentioned: {BotMentioned}.",
            message.Id,
            client.CurrentUser.Id,
            mentionedUserIds,
            botMentionedByRawContent,
            botMentioned);

        if (!botMentioned)
            return;

        var prompt = BuildPrompt(userMessage.Content, client.CurrentUser.Id);
        if (string.IsNullOrWhiteSpace(prompt))
            prompt = "Скажи что-нибудь короткое и милое.";

        if (prompt.Length > options.MaxPromptChars)
            prompt = prompt[..options.MaxPromptChars];

        try
        {
            logger.LogInformation("AI request started. MessageId: {MessageId}. PromptLength: {PromptLength}.", message.Id, prompt.Length);

            using var typing = userMessage.Channel.EnterTypingState();
            using var timeout = new CancellationTokenSource(options.Timeout);
            var reply = await aiChatClient.CreateReplyAsync(prompt, timeout.Token) ?? "Мяу...";

            await userMessage.Channel.SendMessageAsync(
                reply,
                allowedMentions: AllowedMentions.None,
                messageReference: new MessageReference(userMessage.Id));

            logger.LogInformation("AI reply sent. MessageId: {MessageId}. ReplyLength: {ReplyLength}.", message.Id, reply.Length);
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

    private static bool ContainsBotMention(string content, ulong botUserId)
    {
        return content.Contains($"<@{botUserId}>", StringComparison.Ordinal)
            || content.Contains($"<@!{botUserId}>", StringComparison.Ordinal);
    }
}
