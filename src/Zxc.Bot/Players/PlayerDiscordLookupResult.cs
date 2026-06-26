using Zxc.Bot.Auth;

namespace Zxc.Bot.Players;

public sealed record PlayerDiscordLookupResult(
    DiscordLookupStatus Status,
    AuthUserInfo? Player,
    DiscordLinkInfo? Discord,
    string? Error);
