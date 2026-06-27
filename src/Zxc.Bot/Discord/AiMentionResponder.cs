using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace Zxc.Bot.Discord;

public sealed class AiMentionResponder(
    DiscordSocketClient client,
    ILogger<AiMentionResponder> logger)
{
    public async Task HandleAsync(SocketMessage message)
    {
        logger.LogInformation(
            "AI message event. MessageId: {MessageId}. AuthorId: {AuthorId}. AuthorIsBot: {AuthorIsBot}. AuthorIsWebhook: {AuthorIsWebhook}. Source: {Source}. ChannelId: {ChannelId}. ChannelType: {ChannelType}. MessageType: {MessageType}. ContentLength: {ContentLength}. ContentPreview: {ContentPreview}.",
            message.Id,
            message.Author.Id,
            message.Author.IsBot,
            message.Author.IsWebhook,
            message.Source,
            message.Channel.Id,
            message.Channel.GetType().Name,
            message.GetType().Name,
            message.Content?.Length ?? 0,
            BuildContentPreview(message.Content));

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
        var botMentionedByName = StartsWithBotNameMention(userMessage.Content, client.CurrentUser.Username);
        var botMentionedByReply = botMentionedByUsers || botMentionedByRawContent || botMentionedByName
            ? false
            : await IsReplyToBotAsync(userMessage, client.CurrentUser.Id);
        var botMentioned = botMentionedByUsers || botMentionedByRawContent || botMentionedByName || botMentionedByReply;
        logger.LogInformation(
            "AI mention check. MessageId: {MessageId}. BotUserId: {BotUserId}. MentionedUsers: {MentionedUsers}. RawBotMention: {RawBotMention}. NameMention: {NameMention}. ReplyMention: {ReplyMention}. BotMentioned: {BotMentioned}.",
            message.Id,
            client.CurrentUser.Id,
            mentionedUserIds,
            botMentionedByRawContent,
            botMentionedByName,
            botMentionedByReply,
            botMentioned);

        if (!botMentioned)
            return;

        logger.LogInformation("Mention ignored while AI replies are disabled. MessageId: {MessageId}.", message.Id);
    }

    private static string BuildPrompt(string content, ulong botUserId, string botUsername)
    {
        content = content
            .Replace($"<@{botUserId}>", string.Empty, StringComparison.Ordinal)
            .Replace($"<@!{botUserId}>", string.Empty, StringComparison.Ordinal)
            .Trim();

        var nameMention = $"@{botUsername}";
        if (content.StartsWith(nameMention, StringComparison.OrdinalIgnoreCase))
            content = content[nameMention.Length..].TrimStart(' ', ',', ':', ';');

        return content.Trim();
    }

    private static bool ContainsBotMention(string content, ulong botUserId)
    {
        return content.Contains($"<@{botUserId}>", StringComparison.Ordinal)
            || content.Contains($"<@!{botUserId}>", StringComparison.Ordinal);
    }

    private async Task<bool> IsReplyToBotAsync(SocketUserMessage message, ulong botUserId)
    {
        if (message.ReferencedMessage?.Author.Id == botUserId)
            return true;

        var reference = message.Reference;
        if (reference == null || !reference.MessageId.IsSpecified)
            return false;

        var referencedMessageId = reference.MessageId.Value;
        try
        {
            var referencedMessage = await message.Channel.GetMessageAsync(referencedMessageId, CacheMode.AllowDownload);
            return referencedMessage?.Author.Id == botUserId;
        }
        catch (Exception e)
        {
            logger.LogDebug(e, "Failed to resolve referenced message {ReferencedMessageId}.", referencedMessageId);
            return false;
        }
    }

    private static bool StartsWithBotNameMention(string content, string botUsername)
    {
        var mention = $"@{botUsername}";
        if (!content.StartsWith(mention, StringComparison.OrdinalIgnoreCase))
            return false;

        return content.Length == mention.Length
            || char.IsWhiteSpace(content[mention.Length])
            || content[mention.Length] is ',' or ':' or ';';
    }

    private static string BuildContentPreview(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return "<empty>";

        content = content
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();

        return content.Length <= 160 ? content : content[..160];
    }
}
