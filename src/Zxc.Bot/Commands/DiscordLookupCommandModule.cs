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

        var ckey = new SlashCommandOptionBuilder()
            .WithName("ckey")
            .WithDescription("Find SS14 ckey by Discord user")
            .WithType(ApplicationCommandOptionType.SubCommand)
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("user")
                .WithDescription("Discord user")
                .WithType(ApplicationCommandOptionType.User)
                .WithRequired(true));

        return new SlashCommandBuilder()
            .WithName(Name)
            .WithDescription("Discord lookup")
            .AddOption(find)
            .AddOption(ckey)
            .Build();
    }

    public async Task HandleAsync(SocketSlashCommand command)
    {
        var subCommand = command.Data.Options.FirstOrDefault();
        if (subCommand == null)
        {
            await command.RespondAsync(replies.Format(ReplyKind.Denied), ephemeral: true);
            return;
        }

        switch (subCommand.Name)
        {
            case "find":
                await HandleFindAsync(command, subCommand);
                return;
            case "ckey":
                await HandleCkeyAsync(command, subCommand);
                return;
            default:
                await command.RespondAsync(replies.Format(ReplyKind.Denied), ephemeral: true);
                return;
        }
    }

    private async Task HandleFindAsync(SocketSlashCommand command, SocketSlashCommandDataOption subCommand)
    {
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

    private async Task HandleCkeyAsync(SocketSlashCommand command, SocketSlashCommandDataOption subCommand)
    {
        var user = ReadUser(subCommand, "user");

        await command.DeferAsync();

        var result = await lookupService.FindByDiscordIdAsync(user.Id.ToString(), CancellationToken.None);
        await command.ModifyOriginalResponseAsync(message => message.Content = FormatResult(user, result));
    }

    private string FormatResult(string ckey, PlayerDiscordLookupResult result)
    {
        return result.Status switch
        {
            DiscordLookupStatus.Found when result.Player != null && result.Discord != null =>
                replies.Format(ReplyKind.Success, $"<@{result.Discord.DiscordId}>"),
            DiscordLookupStatus.PlayerNotFound =>
                replies.Format(ReplyKind.Empty, $"Player `{ckey}` not found."),
            DiscordLookupStatus.DiscordNotLinked when result.Player != null =>
                replies.Format(ReplyKind.Empty, $"Player `{result.Player.UserName}` (`{result.Player.UserId}`) has no linked Discord."),
            _ => replies.Format(ReplyKind.Error, "Lookup failed."),
        };
    }

    private string FormatResult(IUser user, PlayerCkeyLookupResult result)
    {
        return result.Status switch
        {
            CkeyLookupStatus.Found when result.Player != null =>
                replies.Format(ReplyKind.Success, FormatCkey(result.Player)),
            CkeyLookupStatus.DiscordNotLinked =>
                replies.Format(ReplyKind.Empty, $"{user.Mention} has no linked SS14 account."),
            _ => replies.Format(ReplyKind.Error, "Lookup failed."),
        };
    }

    private static string FormatCkey(DiscordPlayerInfo player)
    {
        return string.IsNullOrWhiteSpace(player.PlayerUserName)
            ? "unknown"
            : player.PlayerUserName;
    }

    private static string ReadString(SocketSlashCommandDataOption subCommand, string optionName)
    {
        var option = subCommand.Options.FirstOrDefault(option => option.Name == optionName)
            ?? throw new InvalidOperationException($"{optionName} option is missing.");

        return option.Value as string
            ?? throw new InvalidOperationException($"{optionName} option is invalid.");
    }

    private static IUser ReadUser(SocketSlashCommandDataOption subCommand, string optionName)
    {
        var option = subCommand.Options.FirstOrDefault(option => option.Name == optionName)
            ?? throw new InvalidOperationException($"{optionName} option is missing.");

        return option.Value as IUser
            ?? throw new InvalidOperationException($"{optionName} option is invalid.");
    }
}
