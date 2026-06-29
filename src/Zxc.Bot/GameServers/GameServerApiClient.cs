using System.Text;
using System.Text.Json;
using Zxc.Bot.Api;

namespace Zxc.Bot.GameServers;

public sealed class GameServerApiClient(HttpClient httpClient) : IGameServerApiClient
{
    private static readonly JsonSerializerOptions ServerRequestJsonOptions = new();

    public Task<ApiResult<GameServerPlayersResponse>> GetPlayersAsync(
        GameServerRecord server,
        CancellationToken cancellationToken)
    {
        return GetAsync<GameServerPlayersResponse>(server, "/admin/players", cancellationToken);
    }

    public Task<ApiResult<GameServerPlaytimeResponse>> GetPlaytimeAsync(
        GameServerRecord server,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return GetAsync<GameServerPlaytimeResponse>(
            server,
            $"/admin/playtime?userId={Uri.EscapeDataString(userId.ToString())}",
            cancellationToken);
    }

    public Task<ApiResult<GameServerPlaytimeJobsResponse>> GetPlaytimeJobsAsync(
        GameServerRecord server,
        CancellationToken cancellationToken)
    {
        return GetAsync<GameServerPlaytimeJobsResponse>(server, "/admin/playtime/jobs", cancellationToken);
    }

    public Task<ApiResult<GameServerRoundStatsResponse>> GetRoundStatsAsync(
        GameServerRecord server,
        string? period,
        int? days,
        CancellationToken cancellationToken)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(period))
            query.Add($"period={Uri.EscapeDataString(period.Trim())}");

        if (days != null)
            query.Add($"days={days.Value}");

        var endpoint = query.Count == 0
            ? "/admin/stats/rounds"
            : $"/admin/stats/rounds?{string.Join("&", query)}";

        return GetAsync<GameServerRoundStatsResponse>(server, endpoint, cancellationToken);
    }

    public Task<ApiResult<GameServerPlaytimeAddResponse>> AddPlaytimeAsync(
        GameServerRecord server,
        GameServerActor actor,
        GameServerPlaytimeAddRequest request,
        CancellationToken cancellationToken)
    {
        return PostActorAsync<GameServerPlaytimeAddRequest, GameServerPlaytimeAddResponse>(
            server,
            "/admin/actions/playtime/add",
            actor,
            request,
            cancellationToken);
    }

    public async Task<ApiResult<TResponse>> GetAsync<TResponse>(
        GameServerRecord server,
        string endpoint,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildUri(server, endpoint));
        request.Headers.TryAddWithoutValidation("Authorization", $"SS14Token {server.Token}");

        return await SendAsync<TResponse>(request, cancellationToken);
    }

    public async Task<ApiResult<TResponse>> PostActorAsync<TRequest, TResponse>(
        GameServerRecord server,
        string endpoint,
        GameServerActor actor,
        TRequest payload,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload, ServerRequestJsonOptions);
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUri(server, endpoint))
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

        request.Headers.TryAddWithoutValidation("Authorization", $"SS14Token {server.Token}");
        request.Headers.TryAddWithoutValidation("Actor", JsonSerializer.Serialize(actor, ServerRequestJsonOptions));

        return await SendAsync<TResponse>(request, cancellationToken);
    }

    private async Task<ApiResult<TResponse>> SendAsync<TResponse>(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            return new ApiResult<TResponse>(response.StatusCode, default, body, body);

        if (typeof(TResponse) == typeof(string))
            return new ApiResult<TResponse>(response.StatusCode, (TResponse)(object)body, body, null);

        if (string.IsNullOrWhiteSpace(body))
            return new ApiResult<TResponse>(response.StatusCode, default, body, "Response body was empty.");

        try
        {
            var value = JsonSerializer.Deserialize<TResponse>(body, GameServerJson.Options);
            if (value == null)
                return new ApiResult<TResponse>(response.StatusCode, default, body, "Response JSON was null.");

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
