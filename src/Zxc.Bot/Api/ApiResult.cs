using System.Net;

namespace Zxc.Bot.Api;

public sealed record ApiResult<T>(
    HttpStatusCode StatusCode,
    T? Value,
    string Body,
    string? Error)
{
    public bool Success => Error is null && (int)StatusCode is >= 200 and <= 299;
}
