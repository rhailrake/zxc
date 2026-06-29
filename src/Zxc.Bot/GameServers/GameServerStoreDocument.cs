namespace Zxc.Bot.GameServers;

public sealed class GameServerStoreDocument
{
    public List<GameServerStoreEntry> Servers { get; set; } = [];
}

public sealed class GameServerStoreEntry
{
    public string Name { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;
}
