namespace Zxc.Bot.Api;

public interface IDeadSpaceApiClient
{
    Task<ApiResult<TResponse>> GetAsync<TResponse>(string endpoint, CancellationToken cancellationToken);

    Task<ApiResult<TResponse>> PostAsync<TRequest, TResponse>(string endpoint, TRequest payload, CancellationToken cancellationToken);

    Task<ApiResult<TResponse>> SendAsync<TResponse>(HttpMethod method, string endpoint, object? payload, CancellationToken cancellationToken);
}
