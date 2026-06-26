using System.Text.Json;

namespace Zxc.Bot.Auth;

public sealed class AuthApiClient(HttpClient httpClient) : IAuthApiClient
{
    public async Task<AuthQueryResult> QueryByNameAsync(string name, CancellationToken cancellationToken)
    {
        var endpoint = $"query/name?name={Uri.EscapeDataString(name)}";
        using var response = await httpClient.GetAsync(endpoint, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            return new AuthQueryResult(response.StatusCode, null, body);

        var user = JsonSerializer.Deserialize<AuthUserInfo>(body, AuthJson.Options);
        return new AuthQueryResult(response.StatusCode, user, body);
    }
}
