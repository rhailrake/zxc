namespace Zxc.Bot.Auth;

public interface IAuthApiClient
{
    Task<AuthQueryResult> QueryByNameAsync(string name, CancellationToken cancellationToken);
}
