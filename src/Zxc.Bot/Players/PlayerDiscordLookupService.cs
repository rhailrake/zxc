using System.Collections.Concurrent;
using System.Net;
using Zxc.Bot.Api;
using Zxc.Bot.Auth;

namespace Zxc.Bot.Players;

public sealed class PlayerDiscordLookupService(
    IAuthApiClient authApiClient,
    IDeadSpaceApiClient deadSpaceApiClient) : IPlayerDiscordLookupService
{
    private static readonly TimeSpan DiscordLookupCacheDuration = TimeSpan.FromMinutes(10);
    private readonly ConcurrentDictionary<string, CachedCkeyLookup> _discordLookupCache = new();

    public async Task<PlayerDiscordLookupResult> FindByCkeyAsync(string ckey, CancellationToken cancellationToken)
    {
        var auth = await authApiClient.QueryByNameAsync(ckey, cancellationToken);
        if (!auth.Success || auth.User == null)
        {
            var status = auth.StatusCode == HttpStatusCode.NotFound
                ? DiscordLookupStatus.PlayerNotFound
                : DiscordLookupStatus.Failed;

            return new PlayerDiscordLookupResult(status, null, null, auth.Body);
        }

        var discord = await deadSpaceApiClient.GetAsync<DiscordLinkInfo>($"{auth.User.UserId}/discord/", cancellationToken);
        if (discord.Success && discord.Value != null)
            return new PlayerDiscordLookupResult(DiscordLookupStatus.Found, auth.User, discord.Value, null);

        var discordStatus = discord.StatusCode == HttpStatusCode.NotFound
            ? DiscordLookupStatus.DiscordNotLinked
            : DiscordLookupStatus.Failed;

        return new PlayerDiscordLookupResult(discordStatus, auth.User, null, discord.Error);
    }

    public async Task<PlayerCkeyLookupResult> FindByDiscordIdAsync(string discordId, CancellationToken cancellationToken)
    {
        if (_discordLookupCache.TryGetValue(discordId, out var cached) &&
            cached.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return cached.Result;
        }

        var player = await deadSpaceApiClient.GetAsync<DiscordPlayerInfo>($"discord/{discordId}/", cancellationToken);
        if (player.Success && player.Value != null)
        {
            if (!string.IsNullOrWhiteSpace(player.Value.PlayerUserName))
                return Cache(discordId, new PlayerCkeyLookupResult(CkeyLookupStatus.Found, player.Value, null));

            var auth = await authApiClient.QueryByUserIdAsync(player.Value.Ss14PlayerId, cancellationToken);
            var playerUserName = auth.User?.UserName;

            return Cache(discordId, new PlayerCkeyLookupResult(
                CkeyLookupStatus.Found,
                player.Value with { PlayerUserName = playerUserName },
                null));
        }

        var status = player.StatusCode == HttpStatusCode.NotFound
            ? CkeyLookupStatus.DiscordNotLinked
            : CkeyLookupStatus.Failed;

        return Cache(discordId, new PlayerCkeyLookupResult(status, null, player.Error));
    }

    private PlayerCkeyLookupResult Cache(string discordId, PlayerCkeyLookupResult result)
    {
        _discordLookupCache[discordId] = new CachedCkeyLookup(
            result,
            DateTimeOffset.UtcNow + DiscordLookupCacheDuration);

        return result;
    }
}
