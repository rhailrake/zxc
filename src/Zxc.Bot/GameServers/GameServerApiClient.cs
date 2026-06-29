using System.Text.Json;
using Zxc.Bot.Api;

namespace Zxc.Bot.GameServers;

public sealed class GameServerApiClient(HttpClient httpClient) : IGameServerApiClient
{
    public Task<ApiResult<GameServerPlayersResponse>> GetPlayersAsync(
        GameServerRecord server,
        CancellationToken cancellationToken)
    {
        return GetAsync<GameServerPlayersResponse>(server, "/admin/players", cancellationToken);
    }

    public async Task<ApiResult<TResponse>> GetAsync<TResponse>(
        GameServerRecord server,
        string endpoint,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildUri(server, endpoint));
        request.Headers.TryAddWithoutValidation("Authorization", $"SS14Token {server.Token}");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            return new ApiResult<TResponse>(response.StatusCode, default, body, body);

        if (typeof(TResponse) == typeof(string))
            return new ApiResult<TResponse>(response.StatusCode, (TResponse)(object)body, body, null);

        if (string.IsNullOrWhiteSpace(body))
            return new ApiResult<TResponse>(response.StatusCode, default, body, null);

        try
        {
            var value = JsonSerializer.Deserialize<TResponse>(body, GameServerJson.Options);
            return new ApiResult<TResponse>(response.StatusCode, value, body, null);
        }
        catch (JsonException e)
        {
            return new ApiResult<TResponse>(response.StatusCode, default, body, e.Message);
        }
    }

    private static Uri BuildUri(GameServerRecord server, string endpoint)
    {
        var baseUrl = server.Url.AbsoluteUri.TrimEnd('/');
        var path = endpoint.TrimStart('/');
        return new Uri($"{baseUrl}/{path}", UriKind.Absolute);
    }
}
