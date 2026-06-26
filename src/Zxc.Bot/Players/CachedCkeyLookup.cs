namespace Zxc.Bot.Players;

public sealed record CachedCkeyLookup(
    PlayerCkeyLookupResult Result,
    DateTimeOffset ExpiresAt);
