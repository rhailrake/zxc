using Discord;
using Discord.WebSocket;
using Zxc.Bot.GameServers;
using Zxc.Bot.Replies;

namespace Zxc.Bot.Commands;

public sealed class ServerCommandModule(
    IGameServerStore serverStore,
    IReplyService replies) : ISlashCommandModule
{
    private const int MaxNameLength = 64;
    private const int MaxUrlLength = 512;
    private const int MaxTokenLength = 1024;

    public string Name => SlashCommandNames.Servers;

    public SlashCommandAccess Access => SlashCommandAccess.Manager;

    public SlashCommandProperties Build()
    {
        var add = new SlashCommandOptionBuilder()
            .WithName("add")
            .WithDescription("Add or update server API access")
            .WithType(ApplicationCommandOptionType.SubCommand)
            .AddOption(BuildNameOption())
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("url")
                .WithDescription("Server URL")
                .WithType(ApplicationCommandOptionType.String)
                .WithRequired(true)
                .WithMinLength(1)
                .WithMaxLength(MaxUrlLength))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("token")
                .WithDescription("Server API token")
                .WithType(ApplicationCommandOptionType.String)
                .WithRequired(true)
                .WithMinLength(1)
                .WithMaxLength(MaxTokenLength));

        var remove = new SlashCommandOptionBuilder()
            .WithName("remove")
            .WithDescription("Remove server API access")
            .WithType(ApplicationCommandOptionType.SubCommand)
            .AddOption(BuildNameOption());

        var list = new SlashCommandOptionBuilder()
            .WithName("list")
            .WithDescription("Show configured servers")
            .WithType(ApplicationCommandOptionType.SubCommand);

        return new SlashCommandBuilder()
            .WithName(Name)
            .WithDescription("Server API access")
            .AddOption(add)
            .AddOption(remove)
            .AddOption(list)
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
            case "add":
                await HandleAddAsync(command, subCommand);
                return;
            case "remove":
                await HandleRemoveAsync(command, subCommand);
                return;
            case "list":
                await HandleListAsync(command);
                return;
            default:
                await command.RespondAsync(replies.Format(ReplyKind.Denied), ephemeral: true);
                return;
        }
    }

    private async Task HandleAddAsync(SocketSlashCommand command, SocketSlashCommandDataOption subCommand)
    {
        if (!TryReadName(subCommand, out var name))
        {
            await command.RespondAsync(replies.Format(ReplyKind.Empty, "Server name can contain only letters, digits, dot, dash and underscore."), ephemeral: true);
            return;
        }

        if (!TryReadUrl(subCommand, out var url))
        {
            await command.RespondAsync(replies.Format(ReplyKind.Empty, "Server URL must be absolute http/https URL."), ephemeral: true);
            return;
        }

        var token = ReadString(subCommand, "token").Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            await command.RespondAsync(replies.Format(ReplyKind.Empty, "Token is empty."), ephemeral: true);
            return;
        }

        var added = await serverStore.AddOrUpdateServerAsync(new GameServerRecord(name, url, token), CancellationToken.None);
        var details = added
            ? $"Added `{name}`: `{url.AbsoluteUri}` (token: hidden)."
            : $"Updated `{name}`: `{url.AbsoluteUri}` (token: hidden).";

        await command.RespondAsync(replies.Format(ReplyKind.Success, details), ephemeral: true);
    }

    private async Task HandleRemoveAsync(SocketSlashCommand command, SocketSlashCommandDataOption subCommand)
    {
        if (!TryReadName(subCommand, out var name))
        {
            await command.RespondAsync(replies.Format(ReplyKind.Empty, "Server name can contain only letters, digits, dot, dash and underscore."), ephemeral: true);
            return;
        }

        var removed = await serverStore.RemoveServerAsync(name, CancellationToken.None);
        var details = removed
            ? $"Removed `{name}`."
            : $"`{name}` was not configured.";

        await command.RespondAsync(replies.Format(removed ? ReplyKind.Success : ReplyKind.Empty, details), ephemeral: true);
    }

    private async Task HandleListAsync(SocketSlashCommand command)
    {
        var servers = await serverStore.GetServersAsync(CancellationToken.None);
        if (servers.Count == 0)
        {
            await command.RespondAsync(replies.Format(ReplyKind.Empty), ephemeral: true);
            return;
        }

        var details = string.Join("\n", servers.Select(server => $"`{server.Name}`: `{server.Url.AbsoluteUri}` (token: hidden)"));
        await command.RespondAsync(replies.Format(ReplyKind.Success, details), ephemeral: true);
    }

    private static SlashCommandOptionBuilder BuildNameOption()
    {
        return new SlashCommandOptionBuilder()
            .WithName("name")
            .WithDescription("Server name")
            .WithType(ApplicationCommandOptionType.String)
            .WithRequired(true)
            .WithMinLength(1)
            .WithMaxLength(MaxNameLength);
    }

    private static bool TryReadName(SocketSlashCommandDataOption subCommand, out string name)
    {
        name = ReadString(subCommand, "name").Trim().ToLowerInvariant();
        if (name.Length is < 1 or > MaxNameLength)
            return false;

        return name.All(character =>
            char.IsAsciiLetterOrDigit(character) ||
            character is '.' or '-' or '_');
    }

    private static bool TryReadUrl(SocketSlashCommandDataOption subCommand, out Uri url)
    {
        var value = ReadString(subCommand, "url").Trim();
        if (Uri.TryCreate(value, UriKind.Absolute, out url!) &&
            (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps))
        {
            return true;
        }

        url = null!;
        return false;
    }

    private static string ReadString(SocketSlashCommandDataOption subCommand, string optionName)
    {
        var option = subCommand.Options.FirstOrDefault(option => option.Name == optionName)
            ?? throw new InvalidOperationException($"{optionName} option is missing.");

        return option.Value as string
            ?? throw new InvalidOperationException($"{optionName} option is invalid.");
    }
}
