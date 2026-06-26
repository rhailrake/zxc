using System.Text.Json;

namespace Zxc.Bot.Auth;

public static class AuthJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
