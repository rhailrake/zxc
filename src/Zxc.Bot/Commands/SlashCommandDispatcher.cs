using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Zxc.Bot.Replies;

namespace Zxc.Bot.Commands;

public sealed class SlashCommandDispatcher
{
    private readonly CommandAccessService _accessService;
    private readonly IReadOnlyDictionary<string, ISlashCommandModule> _modules;
    private readonly IReplyService _replies;
    private readonly ILogger<SlashCommandDispatcher> _logger;

    public SlashCommandDispatcher(
        CommandAccessService accessService,
        IEnumerable<ISlashCommandModule> modules,
        IReplyService replies,
        ILogger<SlashCommandDispatcher> logger)
    {
        _accessService = accessService;
        _modules = modules.ToDictionary(module => module.Name, StringComparer.Ordinal);
        _replies = replies;
        _logger = logger;
    }

    public Task HandleAsync(SocketSlashCommand command)
    {
        _ = Task.Run(() => HandleCommandAsync(command));
        return Task.CompletedTask;
    }

    private async Task HandleCommandAsync(SocketSlashCommand command)
    {
        try
        {
            if (!_modules.TryGetValue(command.CommandName, out var module))
            {
                await command.RespondAsync(_replies.Format(ReplyKind.Denied, "Unknown command."), ephemeral: true);
                return;
            }

            if (!await CanExecuteAsync(command, module))
            {
                await command.RespondAsync(_replies.Format(ReplyKind.Denied, "Access denied."), ephemeral: true);
                return;
            }

            await module.HandleAsync(command);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to handle slash command {CommandName}.", command.CommandName);

            if (command.HasResponded)
                await command.ModifyOriginalResponseAsync(message => message.Content = _replies.Format(ReplyKind.Error, "Command failed."));
            else
                await command.RespondAsync(_replies.Format(ReplyKind.Error, "Command failed."), ephemeral: true);
        }
    }

    private Task<bool> CanExecuteAsync(SocketSlashCommand command, ISlashCommandModule module)
    {
        return module.Access switch
        {
            SlashCommandAccess.Manager => _accessService.CanManageAccessAsync(command.User, module.Name, CancellationToken.None),
            SlashCommandAccess.Role => _accessService.CanUseAsync(command.User, module.Name, CancellationToken.None),
            _ => Task.FromResult(false),
        };
    }
}
