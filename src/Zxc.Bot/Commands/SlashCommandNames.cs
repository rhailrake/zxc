namespace Zxc.Bot.Commands;

public static class SlashCommandNames
{
    public const string Admins = "admins";
    public const string Bot = "bot";
    public const string Discord = "discord";
    public const string Donators = "donators";
    public const string Roles = "roles";
    public const string Servers = "servers";

    public static readonly IReadOnlyList<string> All =
    [
        Admins,
        Bot,
        Discord,
        Donators,
        Roles,
        Servers,
    ];

    public static bool TryNormalize(string commandName, out string normalized)
    {
        foreach (var known in All)
        {
            if (string.Equals(known, commandName, StringComparison.OrdinalIgnoreCase))
            {
                normalized = known;
                return true;
            }
        }

        normalized = string.Empty;
        return false;
    }
}
