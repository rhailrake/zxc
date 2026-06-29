using System.Globalization;
using Discord;
using Discord.WebSocket;
using Zxc.Bot.GameServers;
using Zxc.Bot.Replies;

namespace Zxc.Bot.Commands;

public sealed class GameModeStatsCommandModule(
    IGameServerStore serverStore,
    IGameServerApiClient apiClient,
    IReplyService replies) : ISlashCommandAutocompleteModule
{
    private const int DiscordMessageLimit = 1900;
    private const int MaxRoundStatsDays = 365;
    private static readonly string[] Periods = ["day", "week", "month"];

    public string Name => SlashCommandNames.Gamemodes;

    public SlashCommandAccess Access => SlashCommandAccess.Role;

    public SlashCommandProperties Build()
    {
        var period = new SlashCommandOptionBuilder()
            .WithName("period")
            .WithDescription("Preset period")
            .WithType(ApplicationCommandOptionType.String)
            .WithRequired(false);

        foreach (var value in Periods)
            period.AddChoice(value, value);

        return new SlashCommandBuilder()
            .WithName(Name)
            .WithDescription("Show game mode round statistics")
            .AddOption(BuildServerOption())
            .AddOption(period)
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("days")
                .WithDescription("Custom period in days, 1-365")
                .WithType(ApplicationCommandOptionType.Integer)
                .WithRequired(false))
            .Build();
    }

    public async Task HandleAsync(SocketSlashCommand command)
    {
        await command.DeferAsync();

        var period = ReadOptionalString(command, "period")?.Trim().ToLowerInvariant();
        var days = ReadOptionalLong(command, "days");

        if (!string.IsNullOrWhiteSpace(period) && !Periods.Contains(period, StringComparer.Ordinal))
        {
            await CompleteAsync(command, replies.Format(ReplyKind.Empty, "Invalid period. Use `day`, `week` or `month`."));
            return;
        }

        if (days is < 1 or > MaxRoundStatsDays)
        {
            await CompleteAsync(command, replies.Format(ReplyKind.Empty, $"Days must be between 1 and {MaxRoundStatsDays}."));
            return;
        }

        if (!string.IsNullOrWhiteSpace(period) && days != null)
        {
            await CompleteAsync(command, replies.Format(ReplyKind.Empty, "Use either `period` or `days`, not both."));
            return;
        }

        var server = await ResolveApiServerAsync(command);
        if (server == null)
            return;

        var result = await apiClient.GetRoundStatsAsync(server, period, days == null ? null : (int)days.Value, CancellationToken.None);
        if (!result.Success || result.Value == null)
        {
            var error = TrimError(result.Error);
            var details = $"Failed to fetch game mode stats from `{server.Name}`. HTTP {(int)result.StatusCode}.";
            if (!string.IsNullOrWhiteSpace(error))
                details += $"\n{error}";
            else if (result.Value == null)
                details += "\nResponse body was empty or `null`.";

            await CompleteAsync(command, replies.Format(ReplyKind.Error, details));
            return;
        }

        await CompleteAsync(command, replies.Format(ReplyKind.Success, FormatStats(result.Value)));
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

    private async Task<GameServerRecord?> ResolveApiServerAsync(SocketSlashCommand command)
    {
        var serverName = ReadOptionalString(command, "server");
        if (!string.IsNullOrWhiteSpace(serverName))
        {
            var selectedServer = await serverStore.GetServerAsync(serverName, CancellationToken.None);
            if (selectedServer != null)
                return selectedServer;

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

    private static string FormatStats(GameServerRoundStatsResponse response)
    {
        var lines = new List<string>
        {
            $"Game mode stats on {InlineCode(response.Server)}",
            $"Period: {FormatUtc(response.From)} - {FormatUtc(response.To)}",
            $"Rounds: {response.TotalRounds}",
        };

        if (response.TotalRounds == 0 || response.Modes.Length == 0)
        {
            lines.Add("No rounds found.");
            return string.Join("\n", lines);
        }

        lines.Add("Modes:");
        var hiddenCount = 0;
        foreach (var mode in response.Modes)
        {
            var line = FormatStatLine(mode);
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

    private static string FormatStatLine(GameServerRoundStatsStat stat)
    {
        return $"- {InlineCode(stat.Name)}: {stat.Count} ({FormatPercent(stat.Percent)}%)";
    }

    private static string FormatPercent(double value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string FormatUtc(DateTime value)
    {
        return value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC";
    }

    private static string? ReadOptionalString(SocketSlashCommand command, string optionName)
    {
        return command.Data.Options.FirstOrDefault(option => option.Name == optionName)?.Value as string;
    }

    private static SlashCommandOptionBuilder BuildServerOption()
    {
        return new SlashCommandOptionBuilder()
            .WithName("server")
            .WithDescription("Configured server name")
            .WithType(ApplicationCommandOptionType.String)
            .WithRequired(false)
            .WithMinLength(1)
            .WithMaxLength(64)
            .WithAutocomplete(true);
    }

    private static long? ReadOptionalLong(SocketSlashCommand command, string optionName)
    {
        var value = command.Data.Options.FirstOrDefault(option => option.Name == optionName)?.Value;
        return value switch
        {
            null => null,
            int integer => integer,
            long integer => integer,
            _ => Convert.ToInt64(value, CultureInfo.InvariantCulture),
        };
    }

    private static string InlineCode(string value)
    {
        return $"`{value.Replace('`', '\'')}`";
    }

    private static string TrimError(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
            return string.Empty;

        return error.Length <= 300 ? error : error[..300] + "...";
    }

    private static bool Matches(string value, string? query)
    {
        return string.IsNullOrWhiteSpace(query) ||
            value.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static Task CompleteAsync(SocketSlashCommand command, string content)
    {
        return command.ModifyOriginalResponseAsync(message => message.Content = content);
    }
}
