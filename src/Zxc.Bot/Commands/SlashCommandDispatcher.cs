using Discord;
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

    public Task HandleAutocompleteAsync(SocketAutocompleteInteraction interaction)
    {
        _ = Task.Run(() => HandleAutocompleteInteractionAsync(interaction));
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

            var accessKey = GetAccessKey(command);
            if (!await CanExecuteAsync(command.User, module, accessKey))
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

    private async Task HandleAutocompleteInteractionAsync(SocketAutocompleteInteraction interaction)
    {
        try
        {
            if (!_modules.TryGetValue(interaction.Data.CommandName, out var module) ||
                module is not ISlashCommandAutocompleteModule autocompleteModule ||
                !await CanAutocompleteAsync(interaction.User, module))
            {
                await interaction.RespondAsync([]);
                return;
            }

            var results = await autocompleteModule.GetAutocompleteAsync(interaction);
            await interaction.RespondAsync(results.Take(25));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to handle autocomplete for slash command {CommandName}.", interaction.Data.CommandName);

            if (!interaction.HasResponded)
                await interaction.RespondAsync([]);
        }
    }

    private Task<bool> CanExecuteAsync(IUser user, ISlashCommandModule module, string accessKey)
    {
        return module.Access switch
        {
            SlashCommandAccess.Manager => _accessService.CanManageAccessAsync(user, accessKey, CancellationToken.None),
            SlashCommandAccess.Role => _accessService.CanUseAsync(user, accessKey, CancellationToken.None),
            _ => Task.FromResult(false),
        };
    }

    private Task<bool> CanAutocompleteAsync(IUser user, ISlashCommandModule module)
    {
        return module.Access switch
        {
            SlashCommandAccess.Manager => _accessService.CanManageAnyAsync(user, module.Name, CancellationToken.None),
            SlashCommandAccess.Role => _accessService.CanUseAnyAsync(user, module.Name, CancellationToken.None),
            _ => Task.FromResult(false),
        };
    }

    private static string GetAccessKey(SocketSlashCommand command)
    {
        var parts = new List<string> { command.CommandName };
        var option = command.Data.Options.FirstOrDefault();

        if (option?.Type == ApplicationCommandOptionType.SubCommand)
        {
            parts.Add(option.Name);
        }
        else if (option?.Type == ApplicationCommandOptionType.SubCommandGroup)
        {
            parts.Add(option.Name);
            var subCommand = option.Options.FirstOrDefault();
            if (subCommand?.Type == ApplicationCommandOptionType.SubCommand)
                parts.Add(subCommand.Name);
        }

        return SlashCommandNames.BuildAccessKey(parts.ToArray());
    }
}
