namespace Zxc.Bot.Players;

public sealed record PlayerCkeyLookupResult(
    CkeyLookupStatus Status,
    DiscordPlayerInfo? Player,
    string? Error);
