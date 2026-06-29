using Zxc.Bot.Api;

namespace Zxc.Bot.GameServers;

public interface IGameServerApiClient
{
    Task<ApiResult<TResponse>> GetAsync<TResponse>(
        GameServerRecord server,
        string endpoint,
        CancellationToken cancellationToken);

    Task<ApiResult<TResponse>> PostActorAsync<TRequest, TResponse>(
        GameServerRecord server,
        string endpoint,
        GameServerActor actor,
        TRequest payload,
        CancellationToken cancellationToken);

    Task<ApiResult<GameServerPlayersResponse>> GetPlayersAsync(
        GameServerRecord server,
        CancellationToken cancellationToken);

    Task<ApiResult<GameServerPlaytimeResponse>> GetPlaytimeAsync(
        GameServerRecord server,
        Guid userId,
        CancellationToken cancellationToken);

    Task<ApiResult<GameServerPlaytimeJobsResponse>> GetPlaytimeJobsAsync(
        GameServerRecord server,
        CancellationToken cancellationToken);

    Task<ApiResult<GameServerRoundStatsResponse>> GetRoundStatsAsync(
        GameServerRecord server,
        string? period,
        int? days,
        CancellationToken cancellationToken);

    Task<ApiResult<GameServerPlaytimeAddResponse>> AddPlaytimeAsync(
        GameServerRecord server,
        GameServerActor actor,
        GameServerPlaytimeAddRequest request,
        CancellationToken cancellationToken);
}
