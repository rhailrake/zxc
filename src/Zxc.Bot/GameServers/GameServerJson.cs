using System.Text.Json;

namespace Zxc.Bot.GameServers;

public static class GameServerJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
