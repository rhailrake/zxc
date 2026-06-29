namespace Zxc.Bot.Configuration;

public sealed class GameServerOptions
{
    public const string StorePathEnvironmentVariable = "ZXC_GAME_SERVER_STORE_PATH";

    public required string StorePath { get; init; }

    public static GameServerOptions FromEnvironment()
    {
        return new GameServerOptions
        {
            StorePath = EnvironmentReader.ReadString(StorePathEnvironmentVariable, "/opt/zxc/game-servers.json"),
        };
    }
}
