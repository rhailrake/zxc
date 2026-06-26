using System.Net;
using Zxc.Bot.Api;
using Zxc.Bot.Auth;

namespace Zxc.Bot.Players;

public sealed class PlayerDiscordLookupService(
    IAuthApiClient authApiClient,
    IDeadSpaceApiClient deadSpaceApiClient) : IPlayerDiscordLookupService
{
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
}
