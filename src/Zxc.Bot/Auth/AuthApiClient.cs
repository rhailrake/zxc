using System.Text.Json;

namespace Zxc.Bot.Auth;

public sealed class AuthApiClient(HttpClient httpClient) : IAuthApiClient
{
    public async Task<AuthQueryResult> QueryByNameAsync(string name, CancellationToken cancellationToken)
    {
        return await QueryAsync($"query/name?name={Uri.EscapeDataString(name)}", cancellationToken);
    }

    public async Task<AuthQueryResult> QueryByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await QueryAsync($"query/userid?userid={Uri.EscapeDataString(userId.ToString())}", cancellationToken);
    }

    private async Task<AuthQueryResult> QueryAsync(string endpoint, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(endpoint, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            return new AuthQueryResult(response.StatusCode, null, body);

        var user = JsonSerializer.Deserialize<AuthUserInfo>(body, AuthJson.Options);
        return new AuthQueryResult(response.StatusCode, user, body);
    }
}
