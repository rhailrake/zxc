namespace Zxc.Bot.Players;

public interface IPlayerDiscordLookupService
{
    Task<PlayerDiscordLookupResult> FindByCkeyAsync(string ckey, CancellationToken cancellationToken);
}
