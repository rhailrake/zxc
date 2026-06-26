using Discord;
using Discord.WebSocket;
using Zxc.Bot.Players;
using Zxc.Bot.Replies;

namespace Zxc.Bot.Commands;

public sealed class DiscordLookupCommandModule(
    IPlayerDiscordLookupService lookupService,
    IReplyService replies) : ISlashCommandModule
{
    public string Name => "discord";

    public SlashCommandAccess Access => SlashCommandAccess.Role;

    public SlashCommandProperties Build()
    {
        var find = new SlashCommandOptionBuilder()
            .WithName("find")
            .WithDescription("Find Discord account by SS14 ckey")
            .WithType(ApplicationCommandOptionType.SubCommand)
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("ckey")
                .WithDescription("SS14 ckey")
                .WithType(ApplicationCommandOptionType.String)
                .WithRequired(true)
                .WithMinLength(1)
                .WithMaxLength(64));

        return new SlashCommandBuilder()
            .WithName(Name)
            .WithDescription("Discord lookup")
            .AddOption(find)
            .Build();
    }

    public async Task HandleAsync(SocketSlashCommand command)
    {
        var subCommand = command.Data.Options.FirstOrDefault();
        if (subCommand?.Name != "find")
        {
            await command.RespondAsync(replies.Format(ReplyKind.Denied), ephemeral: true);
            return;
        }

        var ckey = ReadString(subCommand, "ckey").Trim();
        if (string.IsNullOrWhiteSpace(ckey))
        {
            await command.RespondAsync(replies.Format(ReplyKind.Empty), ephemeral: true);
            return;
        }

        await command.DeferAsync();

        var result = await lookupService.FindByCkeyAsync(ckey, CancellationToken.None);
        await command.ModifyOriginalResponseAsync(message => message.Content = FormatResult(ckey, result));
    }

    private string FormatResult(string ckey, PlayerDiscordLookupResult result)
    {
        return result.Status switch
        {
            DiscordLookupStatus.Found when result.Player != null && result.Discord != null =>
                replies.Format(ReplyKind.Success, FormatFound(result.Player.UserName, result.Player.UserId, result.Discord)),
            DiscordLookupStatus.PlayerNotFound =>
                replies.Format(ReplyKind.Empty, $"Player `{ckey}` not found."),
            DiscordLookupStatus.DiscordNotLinked when result.Player != null =>
                replies.Format(ReplyKind.Empty, $"Player `{result.Player.UserName}` (`{result.Player.UserId}`) has no linked Discord."),
            _ => replies.Format(ReplyKind.Error, "Lookup failed."),
        };
    }

    private static string FormatFound(string userName, Guid ss14UserId, DiscordLinkInfo discord)
    {
        return $"""
            Player: `{userName}`
            SS14 ID: `{ss14UserId}`
            Discord: <@{discord.DiscordId}>
            Discord username: `{discord.DiscordUsername}`
            Discord ID: `{discord.DiscordId}`
            """;
    }

    private static string ReadString(SocketSlashCommandDataOption subCommand, string optionName)
    {
        var option = subCommand.Options.FirstOrDefault(option => option.Name == optionName)
            ?? throw new InvalidOperationException($"{optionName} option is missing.");

        return option.Value as string
            ?? throw new InvalidOperationException($"{optionName} option is invalid.");
    }
}
