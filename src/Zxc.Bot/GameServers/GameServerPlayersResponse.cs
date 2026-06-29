namespace Zxc.Bot.GameServers;

public sealed class GameServerPlayersResponse
{
    public string[] Players { get; init; } = [];

    public Dictionary<string, GameServerAdminInfo> Admins { get; init; } = [];
}

public sealed class GameServerAdminInfo
{
    public bool IsActive { get; init; }

    public bool IsStealth { get; init; }

    public string? Title { get; init; }

    public string[] Flags { get; init; } = [];
}
