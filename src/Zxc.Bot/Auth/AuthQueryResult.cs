using System.Net;

namespace Zxc.Bot.Auth;

public sealed record AuthQueryResult(
    HttpStatusCode StatusCode,
    AuthUserInfo? User,
    string Body)
{
    public bool Success => (int)StatusCode is >= 200 and <= 299 && User is not null;
}
