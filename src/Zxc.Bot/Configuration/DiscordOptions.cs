namespace Zxc.Bot.Configuration;

public sealed class DiscordOptions
{
    public const string TokenEnvironmentVariable = "ZXC_DISCORD_TOKEN";
    public const string AllowedRoleIdsEnvironmentVariable = "ZXC_ALLOWED_ROLE_IDS";

    public required string Token { get; init; }

    public required HashSet<ulong> AllowedRoleIds { get; init; }

    public static DiscordOptions FromEnvironment()
    {
        return new DiscordOptions
        {
            Token = EnvironmentReader.ReadRequired(TokenEnvironmentVariable),
            AllowedRoleIds = EnvironmentReader.ReadUlongSet(AllowedRoleIdsEnvironmentVariable),
        };
    }
}
