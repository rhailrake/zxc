namespace Zxc.Bot.Configuration;

public sealed class DonatorRoleOptions
{
    public const string StorePathEnvironmentVariable = "ZXC_DONATOR_ROLE_STORE_PATH";

    public required string StorePath { get; init; }

    public static DonatorRoleOptions FromEnvironment()
    {
        return new DonatorRoleOptions
        {
            StorePath = EnvironmentReader.ReadString(StorePathEnvironmentVariable, "/opt/zxc/donator-roles.json"),
        };
    }
}
