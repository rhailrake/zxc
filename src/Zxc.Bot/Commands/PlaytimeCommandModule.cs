using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Discord;
using Discord.WebSocket;
using Zxc.Bot.Auth;
using Zxc.Bot.GameServers;
using Zxc.Bot.Replies;

namespace Zxc.Bot.Commands;

public sealed partial class PlaytimeCommandModule(
    IAuthApiClient authApiClient,
    IGameServerStore serverStore,
    IGameServerApiClient apiClient,
    IReplyService replies) : ISlashCommandAutocompleteModule
{
    private const int DiscordMessageLimit = 1900;
    private const int MaxAddMinutes = 60 * 24 * 365;
    private const string OverallAlias = "overall";
    private readonly SemaphoreSlim _jobsCacheLock = new(1, 1);
    private GameServerPlaytimeJobsResponse? _jobsCache;

    public string Name => SlashCommandNames.Playtime;

    public SlashCommandAccess Access => SlashCommandAccess.Role;

    public SlashCommandProperties Build()
    {
        var add = new SlashCommandOptionBuilder()
            .WithName("add")
            .WithDescription("Add playtime to player")
            .WithType(ApplicationCommandOptionType.SubCommand)
            .AddOption(BuildCkeyOption())
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("time")
                .WithDescription("Time: 90m, 1h30m, 2ч 15м, 01:30")
                .WithType(ApplicationCommandOptionType.String)
                .WithRequired(true)
                .WithMinLength(1)
                .WithMaxLength(32))
            .AddOption(BuildJobOption(required: false))
            .AddOption(BuildReasonOption());

        var show = new SlashCommandOptionBuilder()
            .WithName("show")
            .WithDescription("Show player playtime")
            .WithType(ApplicationCommandOptionType.SubCommand)
            .AddOption(BuildCkeyOption());

        var jobs = new SlashCommandOptionBuilder()
            .WithName("jobs")
            .WithDescription("Find playtime jobs and trackers")
            .WithType(ApplicationCommandOptionType.SubCommand)
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("query")
                .WithDescription("Job name, id or tracker")
                .WithType(ApplicationCommandOptionType.String)
                .WithRequired(false)
                .WithMinLength(1)
                .WithMaxLength(64));

        return new SlashCommandBuilder()
            .WithName(Name)
            .WithDescription("Playtime tools")
            .AddOption(add)
            .AddOption(show)
            .AddOption(jobs)
            .Build();
    }

    public async Task HandleAsync(SocketSlashCommand command)
    {
        await command.DeferAsync();

        var subCommand = command.Data.Options.FirstOrDefault();
        if (subCommand == null)
        {
            await CompleteAsync(command, replies.Format(ReplyKind.Denied));
            return;
        }

        switch (subCommand.Name)
        {
            case "add":
                await HandleAddAsync(command, subCommand);
                return;
            case "show":
                await HandleShowAsync(command, subCommand);
                return;
            case "jobs":
                await HandleJobsAsync(command, subCommand);
                return;
            default:
                await CompleteAsync(command, replies.Format(ReplyKind.Denied));
                return;
        }
    }

    public async Task<IReadOnlyCollection<AutocompleteResult>> GetAutocompleteAsync(SocketAutocompleteInteraction interaction)
    {
        return interaction.Data.Current.Name switch
        {
            "job" => await GetJobAutocompleteAsync(interaction),
            _ => [],
        };
    }

    private async Task HandleAddAsync(SocketSlashCommand command, SocketSlashCommandDataOption subCommand)
    {
        var context = await BuildContextAsync(command, subCommand);
        if (context == null)
            return;

        var timeText = ReadString(subCommand, "time").Trim();
        if (!TryParseDuration(timeText, out var duration))
        {
            await CompleteAsync(command, replies.Format(ReplyKind.Empty, "Invalid time. Use `90m`, `1h30m`, `2ч 15м` or `01:30`."));
            return;
        }

        var minutes = (int)Math.Ceiling(duration.TotalMinutes);
        if (minutes is < 1 or > MaxAddMinutes)
        {
            await CompleteAsync(command, replies.Format(ReplyKind.Empty, $"Minutes must be between 1 and {MaxAddMinutes}."));
            return;
        }

        var jobText = ReadOptionalString(subCommand, "job");
        var jobs = await GetJobsAsync(context.Server, CancellationToken.None);
        if (jobs == null)
        {
            await CompleteAsync(command, replies.Format(ReplyKind.Error, "Failed to fetch playtime jobs."));
            return;
        }

        if (!TryResolveTracker(jobText, jobs, out var tracker, out var trackerLabel, out var resolveError))
        {
            await CompleteAsync(command, replies.Format(ReplyKind.Empty, resolveError));
            return;
        }

        var actor = BuildActor(command.User);
        var reason = BuildReason(ReadOptionalString(subCommand, "reason"), command.User);
        var request = new GameServerPlaytimeAddRequest
        {
            UserId = context.Player.UserId,
            Tracker = tracker,
            Minutes = minutes,
            Reason = reason,
        };

        var result = await apiClient.AddPlaytimeAsync(context.Server, actor, request, CancellationToken.None);
        if (!result.Success || result.Value == null)
        {
            await CompleteAsync(command, replies.Format(ReplyKind.Error, $"Failed to add playtime. HTTP {(int)result.StatusCode}.\n{TrimError(result.Error)}"));
            return;
        }

        await CompleteAsync(command, replies.Format(
            ReplyKind.Success,
            FormatAddResult(result.Value, trackerLabel)));
    }

    private async Task HandleShowAsync(SocketSlashCommand command, SocketSlashCommandDataOption subCommand)
    {
        var context = await BuildContextAsync(command, subCommand);
        if (context == null)
            return;

        var result = await apiClient.GetPlaytimeAsync(context.Server, context.Player.UserId, CancellationToken.None);
        if (!result.Success || result.Value == null)
        {
            await CompleteAsync(command, replies.Format(ReplyKind.Error, $"Failed to fetch playtime. HTTP {(int)result.StatusCode}."));
            return;
        }

        await CompleteAsync(command, replies.Format(ReplyKind.Success, FormatPlaytime(result.Value)));
    }

    private async Task HandleJobsAsync(SocketSlashCommand command, SocketSlashCommandDataOption subCommand)
    {
        var server = await ResolveApiServerAsync(command);
        if (server == null)
            return;

        var jobs = await GetJobsAsync(server, CancellationToken.None);
        if (jobs == null)
        {
            await CompleteAsync(command, replies.Format(ReplyKind.Error, "Failed to fetch playtime jobs."));
            return;
        }

        var query = ReadOptionalString(subCommand, "query")?.Trim();
        await CompleteAsync(command, replies.Format(ReplyKind.Success, FormatJobs(server.Name, jobs, query)));
    }

    private async Task<IReadOnlyCollection<AutocompleteResult>> GetJobAutocompleteAsync(SocketAutocompleteInteraction interaction)
    {
        var server = await ResolveApiServerAsync();
        if (server == null)
            return [];

        var jobs = await GetJobsAsync(server, CancellationToken.None);
        if (jobs == null)
            return [];

        var query = interaction.Data.Current.Value?.ToString();
        return jobs.Jobs
            .Where(job => Matches(job.Id, query) || Matches(job.Name, query) || Matches(job.PlayTimeTracker, query))
            .OrderBy(job => job.Name, StringComparer.OrdinalIgnoreCase)
            .Take(25)
            .Select(job => new AutocompleteResult(
                TruncateAutocompleteName($"{job.Name} - {job.PlayTimeTracker}"),
                job.PlayTimeTracker))
            .ToArray();
    }

    private async Task<GameServerPlaytimeJobsResponse?> GetJobsAsync(GameServerRecord server, CancellationToken cancellationToken)
    {
        if (_jobsCache != null)
            return _jobsCache;

        await _jobsCacheLock.WaitAsync(cancellationToken);
        try
        {
            if (_jobsCache != null)
                return _jobsCache;

            var result = await apiClient.GetPlaytimeJobsAsync(server, cancellationToken);
            if (!result.Success || result.Value == null)
                return null;

            _jobsCache = result.Value;
            return _jobsCache;
        }
        finally
        {
            _jobsCacheLock.Release();
        }
    }

    private async Task<PlaytimeCommandContext?> BuildContextAsync(
        SocketSlashCommand command,
        SocketSlashCommandDataOption subCommand)
    {
        var server = await ResolveApiServerAsync(command);
        if (server == null)
            return null;

        var ckey = ReadString(subCommand, "ckey").Trim();
        var player = await authApiClient.QueryByNameAsync(ckey, CancellationToken.None);
        if (!player.Success || player.User == null)
        {
            await CompleteAsync(command, replies.Format(ReplyKind.Empty, $"Player `{ckey}` not found."));
            return null;
        }

        return new PlaytimeCommandContext(server, player.User);
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

    private async Task<GameServerRecord?> ResolveApiServerAsync()
    {
        var servers = await serverStore.GetServersAsync(CancellationToken.None);
        return servers.FirstOrDefault();
    }

    private static bool TryResolveTracker(
        string? value,
        GameServerPlaytimeJobsResponse jobs,
        out string tracker,
        out string label,
        out string error)
    {
        tracker = jobs.OverallTracker;
        label = OverallAlias;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(value) ||
            string.Equals(value.Trim(), OverallAlias, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value.Trim(), jobs.OverallTracker, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var query = NormalizeSearch(value);
        var matches = jobs.Jobs
            .Where(job =>
                NormalizeSearch(job.Id) == query ||
                NormalizeSearch(job.Name) == query ||
                NormalizeSearch(job.PlayTimeTracker) == query)
            .ToArray();

        if (matches.Length == 0)
        {
            matches = jobs.Jobs
                .Where(job =>
                    NormalizeSearch(job.Id).Contains(query, StringComparison.Ordinal) ||
                    NormalizeSearch(job.Name).Contains(query, StringComparison.Ordinal) ||
                    NormalizeSearch(job.PlayTimeTracker).Contains(query, StringComparison.Ordinal))
                .Take(6)
                .ToArray();
        }

        if (matches.Length == 1)
        {
            tracker = matches[0].PlayTimeTracker;
            label = $"{matches[0].Name} (`{matches[0].PlayTimeTracker}`)";
            return true;
        }

        if (matches.Length > 1)
        {
            error = "More than one job matched:\n" + string.Join("\n", matches.Select(FormatJobLine));
            return false;
        }

        error = $"Job/tracker `{value}` not found. Use `/playtime jobs query:{value}` to search.";
        return false;
    }

    private static string FormatPlaytime(GameServerPlaytimeResponse playtime)
    {
        var lines = new List<string>
        {
            $"`{playtime.Player.UserName}`",
            $"Overall: {FormatSeconds(playtime.OverallSeconds)}",
        };

        var trackers = playtime.Trackers
            .Where(tracker => tracker.Seconds > 0 && tracker.Job != null)
            .OrderByDescending(tracker => tracker.Seconds)
            .Take(12)
            .ToArray();

        if (trackers.Length == 0)
            return string.Join("\n", lines);

        lines.Add("Top jobs:");
        foreach (var tracker in trackers)
            lines.Add($"- {tracker.Job!.Name}: {FormatSeconds(tracker.Seconds)}");

        return string.Join("\n", lines);
    }

    private static string FormatAddResult(GameServerPlaytimeAddResponse response, string trackerLabel)
    {
        var lines = new List<string>
        {
            "Added playtime",
            $"Player: `{response.Player.UserName}` (`{response.Player.UserId}`)",
            $"Tracker: {trackerLabel}",
        };

        foreach (var entry in response.Entries)
        {
            lines.Add($"Added: {FormatSeconds(entry.AddedSeconds)}");
            lines.Add($"Before: {FormatSeconds(entry.PreviousSeconds)}");
            lines.Add($"After: {FormatSeconds(entry.NewSeconds)}");
        }

        if (!string.IsNullOrWhiteSpace(response.Reason))
            lines.Add($"Reason: {response.Reason}");

        return string.Join("\n", lines);
    }

    private static string FormatJobs(
        string serverName,
        GameServerPlaytimeJobsResponse response,
        string? query)
    {
        var jobs = response.Jobs.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var normalized = NormalizeSearch(query);
            jobs = jobs.Where(job =>
                NormalizeSearch(job.Id).Contains(normalized, StringComparison.Ordinal) ||
                NormalizeSearch(job.Name).Contains(normalized, StringComparison.Ordinal) ||
                NormalizeSearch(job.PlayTimeTracker).Contains(normalized, StringComparison.Ordinal));
        }

        var lines = new List<string>
        {
            $"Playtime jobs on `{serverName}`",
            $"Overall tracker: `{response.OverallTracker}`",
        };

        var hiddenCount = 0;
        foreach (var job in jobs.OrderBy(job => job.Department?.Name).ThenBy(job => job.Name))
        {
            var line = FormatJobLine(job);
            var candidate = string.Join("\n", lines.Append(line));
            if (candidate.Length > DiscordMessageLimit)
            {
                hiddenCount++;
                continue;
            }

            lines.Add(line);
        }

        if (lines.Count == 2)
            lines.Add("No jobs matched.");

        if (hiddenCount > 0)
            lines.Add($"+{hiddenCount} more");

        return string.Join("\n", lines);
    }

    private static string FormatJobLine(GameServerPlaytimeJob job)
    {
        var department = job.Department?.Name ?? "No department";
        return $"- {job.Name} - `{job.Id}` / `{job.PlayTimeTracker}` ({department})";
    }

    private static bool TryParseDuration(string value, out TimeSpan duration)
    {
        value = value.Trim().ToLowerInvariant();
        duration = TimeSpan.Zero;

        if (TimeSpan.TryParseExact(value, ["h\\:m", "h\\:mm", "hh\\:m", "hh\\:mm", "d\\.hh\\:mm"], CultureInfo.InvariantCulture, out duration))
            return duration > TimeSpan.Zero;

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var plainMinutes))
        {
            duration = TimeSpan.FromMinutes(plainMinutes);
            return duration > TimeSpan.Zero;
        }

        var totalMinutes = 0d;
        var matched = false;
        var matches = DurationPartRegex().Matches(value);
        foreach (Match match in matches)
        {
            if (!double.TryParse(match.Groups["value"].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var amount))
                return false;

            totalMinutes += match.Groups["unit"].Value switch
            {
                "d" or "day" or "days" or "д" or "дн" => amount * 24 * 60,
                "h" or "hour" or "hours" or "ч" or "час" or "часа" or "часов" => amount * 60,
                "m" or "min" or "mins" or "minute" or "minutes" or "м" or "мин" or "минута" or "минут" => amount,
                _ => 0,
            };
            matched = true;
        }

        duration = TimeSpan.FromMinutes(totalMinutes);
        var remainder = DurationPartRegex().Replace(value, string.Empty);
        return matched && duration > TimeSpan.Zero && string.IsNullOrWhiteSpace(remainder);
    }

    private static string BuildReason(string? reason, IUser user)
    {
        var actor = $"{user.Username} ({user.Id})";
        return string.IsNullOrWhiteSpace(reason)
            ? $"Discord playtime transfer by {actor}"
            : $"{reason.Trim()} | Discord: {actor}";
    }

    private static GameServerActor BuildActor(IUser user)
    {
        return new GameServerActor
        {
            Guid = BuildStableActorGuid(user.Id),
            Name = $"{user.Username} ({user.Id})",
        };
    }

    private static Guid BuildStableActorGuid(ulong userId)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes($"discord:{userId}"));
        return new Guid(bytes);
    }

    private static string FormatSeconds(long seconds)
    {
        var time = TimeSpan.FromSeconds(seconds);
        var parts = new List<string>();

        if (time.Days > 0)
            parts.Add($"{time.Days}d");
        if (time.Hours > 0)
            parts.Add($"{time.Hours}h");
        if (time.Minutes > 0 || parts.Count == 0)
            parts.Add($"{time.Minutes}m");

        return string.Join(" ", parts);
    }

    private static string TrimError(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
            return string.Empty;

        return error.Length <= 300 ? error : error[..300] + "...";
    }

    private static string NormalizeSearch(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    private static SlashCommandOptionBuilder BuildCkeyOption()
    {
        return new SlashCommandOptionBuilder()
            .WithName("ckey")
            .WithDescription("SS14 ckey")
            .WithType(ApplicationCommandOptionType.String)
            .WithRequired(true)
            .WithMinLength(1)
            .WithMaxLength(64);
    }

    private static SlashCommandOptionBuilder BuildJobOption(bool required)
    {
        return new SlashCommandOptionBuilder()
            .WithName("job")
            .WithDescription("Job id, job name or tracker. Empty = overall")
            .WithType(ApplicationCommandOptionType.String)
            .WithRequired(required)
            .WithMinLength(1)
            .WithMaxLength(96)
            .WithAutocomplete(true);
    }

    private static SlashCommandOptionBuilder BuildReasonOption()
    {
        return new SlashCommandOptionBuilder()
            .WithName("reason")
            .WithDescription("Reason")
            .WithType(ApplicationCommandOptionType.String)
            .WithRequired(false)
            .WithMinLength(1)
            .WithMaxLength(512);
    }

    private static string ReadString(SocketSlashCommandDataOption subCommand, string optionName)
    {
        var option = subCommand.Options.FirstOrDefault(option => option.Name == optionName)
            ?? throw new InvalidOperationException($"{optionName} option is missing.");

        return option.Value as string
            ?? throw new InvalidOperationException($"{optionName} option is invalid.");
    }

    private static string? ReadOptionalString(SocketSlashCommandDataOption subCommand, string optionName)
    {
        return subCommand.Options.FirstOrDefault(option => option.Name == optionName)?.Value as string;
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

    private static string TruncateAutocompleteName(string value)
    {
        return value.Length <= 100 ? value : value[..97] + "...";
    }

    [GeneratedRegex(@"(?<value>\d+(?:[\.,]\d+)?)\s*(?<unit>d|day|days|д|дн|h|hour|hours|ч|час|часа|часов|m|min|mins|minute|minutes|м|мин|минута|минут)", RegexOptions.IgnoreCase)]
    private static partial Regex DurationPartRegex();

    private sealed record PlaytimeCommandContext(
        GameServerRecord Server,
        AuthUserInfo Player);

}
