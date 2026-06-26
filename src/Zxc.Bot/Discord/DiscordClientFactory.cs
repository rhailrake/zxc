using Discord;
using Discord.WebSocket;

namespace Zxc.Bot.Discord;

public static class DiscordClientFactory
{
    public static DiscordSocketClient Create()
    {
        return new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMembers | GatewayIntents.GuildMessages | GatewayIntents.MessageContent,
            LogLevel = LogSeverity.Info,
            AlwaysDownloadUsers = false,
        });
    }
}
