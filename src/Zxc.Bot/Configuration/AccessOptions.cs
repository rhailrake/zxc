namespace Zxc.Bot.Configuration;

public sealed class AccessOptions
{
    public const string StorePathEnvironmentVariable = "ZXC_ACCESS_STORE_PATH";

    public required string StorePath { get; init; }

    public static AccessOptions FromEnvironment()
    {
        return new AccessOptions
        {
            StorePath = EnvironmentReader.ReadString(StorePathEnvironmentVariable, "/opt/zxc/access.json"),
        };
    }
}
