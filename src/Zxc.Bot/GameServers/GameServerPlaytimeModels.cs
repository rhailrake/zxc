namespace Zxc.Bot.GameServers;

public sealed class GameServerPlaytimeResponse
{
    public required GameServerPlaytimePlayer Player { get; init; }

    public required long OverallSeconds { get; init; }

    public required GameServerTrackerTime[] Trackers { get; init; }
}

public sealed class GameServerPlaytimeJobsResponse
{
    public required string OverallTracker { get; init; }

    public required GameServerPlaytimeJob[] Jobs { get; init; }
}

public sealed class GameServerPlaytimeAddRequest
{
    public required Guid UserId { get; init; }

    public string? Tracker { get; init; }

    public int? Minutes { get; init; }

    public string? Reason { get; init; }
}

public sealed class GameServerPlaytimeAddResponse
{
    public required GameServerPlaytimePlayer Player { get; init; }

    public required string? Reason { get; init; }

    public required GameServerPlaytimeAddEntry[] Entries { get; init; }
}

public sealed class GameServerPlaytimePlayer
{
    public required Guid UserId { get; init; }

    public required string UserName { get; init; }
}

public sealed class GameServerTrackerTime
{
    public required string Tracker { get; init; }

    public required long Seconds { get; init; }

    public required GameServerPlaytimeJob? Job { get; init; }
}

public sealed class GameServerPlaytimeJob
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string PlayTimeTracker { get; init; }

    public required GameServerPlaytimeDepartment? Department { get; init; }

    public required bool SetPreference { get; init; }
}

public sealed class GameServerPlaytimeDepartment
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required bool Primary { get; init; }
}

public sealed class GameServerPlaytimeAddEntry
{
    public required string Tracker { get; init; }

    public required long AddedSeconds { get; init; }

    public required long PreviousSeconds { get; init; }

    public required long NewSeconds { get; init; }
}
