using System.Text.Json;

namespace Zxc.Bot.Ai;

public static class AiJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
