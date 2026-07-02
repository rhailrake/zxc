namespace Zxc.Bot.Commands;

public static class SlashCommandNames
{
    public const string Admins = "admins";
    public const string Bot = "bot";
    public const string Discord = "discord";
    public const string Donators = "donators";
    public const string Gamemodes = "gamemodes";
    public const string Playtime = "playtime";
    public const string Roles = "roles";
    public const string Servers = "servers";

    public static readonly IReadOnlyList<string> All =
    [
        Admins,
        Bot,
        Discord,
        Donators,
        Gamemodes,
        Playtime,
        Roles,
        Servers,
    ];

    public static readonly IReadOnlyList<string> AllAccessKeys =
    [
        Admins,
        Bot,
        BuildAccessKey(Bot, "version"),
        BuildAccessKey(Bot, "update"),
        BuildAccessKey(Bot, "restart"),
        Discord,
        BuildAccessKey(Discord, "find"),
        BuildAccessKey(Discord, "ckey"),
        Donators,
        BuildAccessKey(Donators, "add-role"),
        BuildAccessKey(Donators, "remove-role"),
        BuildAccessKey(Donators, "roles"),
        BuildAccessKey(Donators, "list"),
        Gamemodes,
        Playtime,
        BuildAccessKey(Playtime, "add"),
        BuildAccessKey(Playtime, "show"),
        Roles,
        BuildAccessKey(Roles, "add"),
        BuildAccessKey(Roles, "remove"),
        BuildAccessKey(Roles, "list"),
        Servers,
        BuildAccessKey(Servers, "add"),
        BuildAccessKey(Servers, "remove"),
        BuildAccessKey(Servers, "list"),
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

    public static bool TryNormalizeAccessKey(string value, out string normalized)
    {
        var key = NormalizeAccessKey(value);
        foreach (var known in AllAccessKeys)
        {
            if (string.Equals(known, key, StringComparison.OrdinalIgnoreCase))
            {
                normalized = known;
                return true;
            }
        }

        normalized = string.Empty;
        return false;
    }

    public static string BuildAccessKey(params string[] parts)
    {
        return string.Join(" ", parts
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => part.Trim().ToLowerInvariant()));
    }

    public static IReadOnlyList<string> GetParentAccessKeys(string accessKey)
    {
        accessKey = NormalizeAccessKey(accessKey);
        var parts = accessKey.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var keys = new List<string>();
        for (var length = parts.Length; length >= 1; length--)
            keys.Add(string.Join(" ", parts.Take(length)));

        return keys;
    }

    private static string NormalizeAccessKey(string value)
    {
        value = value.Trim().TrimStart('/');
        value = value.Replace('/', ' ').Replace(':', ' ');
        return string.Join(" ", value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.ToLowerInvariant()));
    }
}
