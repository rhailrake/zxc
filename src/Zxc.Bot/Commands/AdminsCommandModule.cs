using Discord;
using Discord.WebSocket;
using Zxc.Bot.GameServers;
using Zxc.Bot.Replies;

namespace Zxc.Bot.Commands;

public sealed class AdminsCommandModule(
    IGameServerStore serverStore,
    IGameServerApiClient apiClient,
    IReplyService replies) : ISlashCommandAutocompleteModule
{
    private const int DiscordMessageLimit = 1900;

    public string Name => SlashCommandNames.Admins;

    public SlashCommandAccess Access => SlashCommandAccess.Role;

    public SlashCommandProperties Build()
    {
        return new SlashCommandBuilder()
            .WithName(Name)
            .WithDescription("Show online admins on a server")
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("server")
                .WithDescription("Configured server name")
                .WithType(ApplicationCommandOptionType.String)
                .WithRequired(false)
                .WithMinLength(1)
                .WithMaxLength(64)
                .WithAutocomplete(true))
            .Build();
    }

    public async Task<IReadOnlyCollection<AutocompleteResult>> GetAutocompleteAsync(SocketAutocompleteInteraction interaction)
    {
        if (interaction.Data.Current.Name != "server")
            return [];

        var query = interaction.Data.Current.Value?.ToString();
        var servers = await serverStore.GetServersAsync(CancellationToken.None);
        return servers
            .Where(server => Matches(server.Name, query))
            .OrderBy(server => server.Name, StringComparer.OrdinalIgnoreCase)
            .Take(25)
            .Select(server => new AutocompleteResult(server.Name, server.Name))
            .ToArray();
    }

    public async Task HandleAsync(SocketSlashCommand command)
    {
        await command.DeferAsync(ephemeral: true);

        var server = await ResolveServerAsync(command);
        if (server == null)
            return;

        var result = await apiClient.GetPlayersAsync(server, CancellationToken.None);
        if (!result.Success || result.Value == null)
        {
            var details = $"Failed to fetch admins from `{server.Name}`. HTTP {(int)result.StatusCode}.";
            await CompleteAsync(command, replies.Format(ReplyKind.Error, details));
            return;
        }

        var admins = result.Value.Admins
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (admins.Length == 0)
        {
            await CompleteAsync(command, replies.Format(ReplyKind.Empty, $"No online admins on `{server.Name}`."));
            return;
        }

        await CompleteAsync(command, replies.Format(ReplyKind.Success, FormatAdmins(server.Name, admins)));
    }

    private async Task<GameServerRecord?> ResolveServerAsync(SocketSlashCommand command)
    {
        var serverName = ReadOptionalString(command, "server");
        if (!string.IsNullOrWhiteSpace(serverName))
        {
            var server = await serverStore.GetServerAsync(serverName, CancellationToken.None);
            if (server != null)
                return server;

            await CompleteAsync(command, replies.Format(ReplyKind.Empty, $"Server `{serverName}` is not configured."));
            return null;
        }

        var servers = await serverStore.GetServersAsync(CancellationToken.None);
        if (servers.Count == 1)
            return servers.Single();

        if (servers.Count == 0)
        {
            await CompleteAsync(command, replies.Format(ReplyKind.Empty, "No servers configured."));
            return null;
        }

        var details = "Specify server: " + string.Join(", ", servers.Select(server => InlineCode(server.Name)));
        await CompleteAsync(command, replies.Format(ReplyKind.Empty, details));
        return null;
    }

    private static string FormatAdmins(
        string serverName,
        IReadOnlyCollection<KeyValuePair<string, GameServerAdminInfo>> admins)
    {
        var header = $"Online admins on `{serverName}`: {admins.Count}";
        var lines = new List<string> { header };
        var hiddenCount = 0;

        foreach (var (name, admin) in admins)
        {
            var line = FormatAdmin(name, admin);
            var candidate = string.Join("\n", lines.Append(line));
            if (candidate.Length > DiscordMessageLimit)
            {
                hiddenCount++;
                continue;
            }

            lines.Add(line);
        }

        if (hiddenCount > 0)
            lines.Add($"+{hiddenCount} more");

        return string.Join("\n", lines);
    }

    private static string FormatAdmin(string name, GameServerAdminInfo admin)
    {
        var states = new List<string>
        {
            admin.IsActive ? "active" : "deadminned",
        };

        if (admin.IsStealth)
            states.Add("stealth");

        var title = string.IsNullOrWhiteSpace(admin.Title)
            ? "no title"
            : admin.Title.Trim();

        return $"- {InlineCode(name)} - {title} ({string.Join(", ", states)})";
    }

    private static string? ReadOptionalString(SocketSlashCommand command, string optionName)
    {
        return command.Data.Options.FirstOrDefault(option => option.Name == optionName)?.Value as string;
    }

    private static string InlineCode(string value)
    {
        return $"`{value.Replace('`', '\'')}`";
    }

    private static Task CompleteAsync(SocketSlashCommand command, string content)
    {
        return command.ModifyOriginalResponseAsync(message => message.Content = content);
    }

    private static bool Matches(string value, string? query)
    {
        return string.IsNullOrWhiteSpace(query) ||
            value.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
