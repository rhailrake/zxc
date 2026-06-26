using System.Net.Http.Json;
using System.Text.Json;

namespace Zxc.Bot.Api;

public sealed class DeadSpaceApiClient(HttpClient httpClient) : IDeadSpaceApiClient
{
    public Task<ApiResult<TResponse>> GetAsync<TResponse>(string endpoint, CancellationToken cancellationToken)
    {
        return SendAsync<TResponse>(HttpMethod.Get, endpoint, null, cancellationToken);
    }

    public Task<ApiResult<TResponse>> PostAsync<TRequest, TResponse>(string endpoint, TRequest payload, CancellationToken cancellationToken)
    {
        return SendAsync<TResponse>(HttpMethod.Post, endpoint, payload, cancellationToken);
    }

    public async Task<ApiResult<TResponse>> SendAsync<TResponse>(HttpMethod method, string endpoint, object? payload, CancellationToken cancellationToken)
    {
        using var content = payload is null
            ? null
            : JsonContent.Create(payload, options: ZxcJson.Options);

        using var request = new HttpRequestMessage(method, BuildUri(endpoint))
        {
            Content = content,
        };

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
            var value = JsonSerializer.Deserialize<TResponse>(body, ZxcJson.Options);
            return new ApiResult<TResponse>(response.StatusCode, value, body, null);
        }
        catch (JsonException e)
        {
            return new ApiResult<TResponse>(response.StatusCode, default, body, e.Message);
        }
    }

    private Uri BuildUri(string endpoint)
    {
        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var absoluteUri))
            return absoluteUri;

        if (httpClient.BaseAddress == null)
            throw new InvalidOperationException("API base URL is not configured.");

        return new Uri(httpClient.BaseAddress, endpoint.TrimStart('/'));
    }
}
