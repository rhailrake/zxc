namespace Zxc.Bot.GameServers;

public sealed class GameServerRoundStatsResponse
{
    public required string Server { get; init; }

    public required DateTimeOffset From { get; init; }

    public required DateTimeOffset To { get; init; }

    public required int TotalRounds { get; init; }

    public required GameServerRoundStatsStat[] Modes { get; init; }

    public required GameServerRoundStatsStat[] Maps { get; init; }

    public required GameServerRoundStatsRound[] Rounds { get; init; }
}

public sealed class GameServerRoundStatsStat
{
    public required string Name { get; init; }

    public required int Count { get; init; }

    public required double Percent { get; init; }
}

public sealed class GameServerRoundStatsRound
{
    public required int RoundId { get; init; }

    public required DateTimeOffset StartedAt { get; init; }

    public required string GameMode { get; init; }

    public required string Map { get; init; }

    public required int? PlayerCount { get; init; }
}
