using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace Zxc.Bot.Commands;

public sealed class SlashCommandRegistrar(
    DiscordSocketClient client,
    IEnumerable<ISlashCommandModule> modules,
    ILogger<SlashCommandRegistrar> logger)
{
    private bool _registered;

    public async Task RegisterAsync()
    {
        if (_registered)
            return;

        var commands = modules
            .OrderBy(module => module.Name, StringComparer.Ordinal)
            .Select(module => module.Build())
            .ToArray();

        await client.BulkOverwriteGlobalApplicationCommandsAsync(commands);

        _registered = true;
        logger.LogInformation("Registered {CommandCount} slash commands.", commands.Length);
    }
}
