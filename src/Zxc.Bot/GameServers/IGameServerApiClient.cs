using Zxc.Bot.Api;

namespace Zxc.Bot.GameServers;

public interface IGameServerApiClient
{
    Task<ApiResult<TResponse>> GetAsync<TResponse>(
        GameServerRecord server,
        string endpoint,
        CancellationToken cancellationToken);

    Task<ApiResult<GameServerPlayersResponse>> GetPlayersAsync(
        GameServerRecord server,
        CancellationToken cancellationToken);
}
