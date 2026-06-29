using Discord;
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

        var guilds = client.Guilds
            .OrderBy(guild => guild.Id)
            .ToArray();

        if (guilds.Length == 0)
        {
            await client.BulkOverwriteGlobalApplicationCommandsAsync(commands);
            logger.LogInformation("Registered {CommandCount} global slash commands.", commands.Length);
        }
        else
        {
            await client.BulkOverwriteGlobalApplicationCommandsAsync(Array.Empty<ApplicationCommandProperties>());

            foreach (var guild in guilds)
                await guild.BulkOverwriteApplicationCommandAsync(commands);

            logger.LogInformation(
                "Registered {CommandCount} guild slash commands in {GuildCount} guilds.",
                commands.Length,
                guilds.Length);
        }

        _registered = true;
    }
}
