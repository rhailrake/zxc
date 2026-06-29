using System.Globalization;
using Discord;
using Discord.WebSocket;
using Zxc.Bot.GameServers;
using Zxc.Bot.Replies;

namespace Zxc.Bot.Commands;

public sealed class GameModeStatsCommandModule(
    IGameServerStore serverStore,
    IGameServerApiClient apiClient,
    IReplyService replies) : ISlashCommandModule
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
        await command.DeferAsync(ephemeral: true);

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
            await CompleteAsync(command, replies.Format(ReplyKind.Error, $"Failed to fetch game mode stats from `{server.Name}`. HTTP {(int)result.StatusCode}.\n{TrimError(result.Error)}"));
            return;
        }

        await CompleteAsync(command, replies.Format(ReplyKind.Success, FormatStats(result.Value)));
    }

    private async Task<GameServerRecord?> ResolveApiServerAsync(SocketSlashCommand command)
    {
        var servers = await serverStore.GetServersAsync(CancellationToken.None);
        var server = servers.FirstOrDefault();
        if (server != null)
            return server;

        await CompleteAsync(command, replies.Format(ReplyKind.Empty, "No servers configured."));
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

    private static string FormatUtc(DateTimeOffset value)
    {
        return value.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC";
    }

    private static string? ReadOptionalString(SocketSlashCommand command, string optionName)
    {
        return command.Data.Options.FirstOrDefault(option => option.Name == optionName)?.Value as string;
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

    private static Task CompleteAsync(SocketSlashCommand command, string content)
    {
        return command.ModifyOriginalResponseAsync(message => message.Content = content);
    }
}
