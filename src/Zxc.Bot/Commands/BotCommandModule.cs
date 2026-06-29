using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Zxc.Bot.Replies;
using Zxc.Bot.SelfUpdate;

namespace Zxc.Bot.Commands;

public sealed class BotCommandModule(
    IBotMaintenanceService maintenanceService,
    IReplyService replies,
    ILogger<BotCommandModule> logger) : ISlashCommandModule
{
    public string Name => SlashCommandNames.Bot;

    public SlashCommandAccess Access => SlashCommandAccess.Role;

    public SlashCommandProperties Build()
    {
        var version = new SlashCommandOptionBuilder()
            .WithName("version")
            .WithDescription("Show bot version")
            .WithType(ApplicationCommandOptionType.SubCommand);

        var update = new SlashCommandOptionBuilder()
            .WithName("update")
            .WithDescription("Update and restart bot")
            .WithType(ApplicationCommandOptionType.SubCommand);

        var restart = new SlashCommandOptionBuilder()
            .WithName("restart")
            .WithDescription("Restart bot")
            .WithType(ApplicationCommandOptionType.SubCommand);

        return new SlashCommandBuilder()
            .WithName(Name)
            .WithDescription("Bot maintenance")
            .AddOption(version)
            .AddOption(update)
            .AddOption(restart)
            .Build();
    }

    public async Task HandleAsync(SocketSlashCommand command)
    {
        var subCommand = command.Data.Options.FirstOrDefault();
        if (subCommand == null)
        {
            await command.RespondAsync(replies.Format(ReplyKind.Denied, "Unknown bot command."), ephemeral: true);
            return;
        }

        switch (subCommand.Name)
        {
            case "version":
                await HandleVersionAsync(command);
                return;
            case "update":
                await HandleUpdateAsync(command);
                return;
            case "restart":
                await HandleRestartAsync(command);
                return;
            default:
                await command.RespondAsync(replies.Format(ReplyKind.Denied, "Unknown bot command."), ephemeral: true);
                return;
        }
    }

    private async Task HandleVersionAsync(SocketSlashCommand command)
    {
        await command.DeferAsync(ephemeral: true);

        try
        {
            var version = await maintenanceService.GetVersionAsync(CancellationToken.None);
            await command.ModifyOriginalResponseAsync(message => message.Content = replies.Format(ReplyKind.Success, FormatVersion(version)));
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get bot version.");
            await command.ModifyOriginalResponseAsync(message => message.Content = replies.Format(ReplyKind.Error, "Failed to get bot version."));
        }
    }

    private async Task HandleUpdateAsync(SocketSlashCommand command)
    {
        try
        {
            await maintenanceService.StartUpdateAsync();
            await command.RespondAsync(replies.Format(ReplyKind.Success, "Update started."), ephemeral: true);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to start bot update.");
            await command.RespondAsync(replies.Format(ReplyKind.Error, "Failed to start update."), ephemeral: true);
        }
    }

    private async Task HandleRestartAsync(SocketSlashCommand command)
    {
        try
        {
            await maintenanceService.StartRestartAsync();
            await command.RespondAsync(replies.Format(ReplyKind.Success, "Restart started."), ephemeral: true);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to start bot restart.");
            await command.RespondAsync(replies.Format(ReplyKind.Error, "Failed to start restart."), ephemeral: true);
        }
    }

    private static string FormatVersion(BotVersion version)
    {
        return $"""
            `zxc` version
            Branch: `{version.Branch ?? "unknown"}`
            Commit: `{version.Commit ?? "unknown"}`
            """;
    }
}
