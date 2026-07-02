using Discord;
using Discord.WebSocket;
using Zxc.Bot.Access;
using Zxc.Bot.Replies;

namespace Zxc.Bot.Commands;

public sealed class RoleCommandModule(
    IRoleAccessStore roleAccessStore,
    CommandAccessService accessService,
    IReplyService replies) : ISlashCommandAutocompleteModule
{
    public string Name => SlashCommandNames.Roles;

    public SlashCommandAccess Access => SlashCommandAccess.Manager;

    public SlashCommandProperties Build()
    {
        var roleOption = new SlashCommandOptionBuilder()
            .WithName("role")
            .WithDescription("Role")
            .WithType(ApplicationCommandOptionType.Role)
            .WithRequired(true);

        var add = new SlashCommandOptionBuilder()
            .WithName("add")
            .WithDescription("Allow role to use a command")
            .WithType(ApplicationCommandOptionType.SubCommand)
            .AddOption(CloneCommandOption(required: true))
            .AddOption(CloneRoleOption());

        var remove = new SlashCommandOptionBuilder()
            .WithName("remove")
            .WithDescription("Remove role from command access")
            .WithType(ApplicationCommandOptionType.SubCommand)
            .AddOption(CloneCommandOption(required: true))
            .AddOption(CloneRoleOption());

        var list = new SlashCommandOptionBuilder()
            .WithName("list")
            .WithDescription("Show command access roles")
            .WithType(ApplicationCommandOptionType.SubCommand)
            .AddOption(CloneCommandOption(required: false));

        return new SlashCommandBuilder()
            .WithName(Name)
            .WithDescription("Command access roles")
            .AddOption(add)
            .AddOption(remove)
            .AddOption(list)
            .Build();

        SlashCommandOptionBuilder CloneCommandOption(bool required)
        {
            return new SlashCommandOptionBuilder()
                .WithName("command")
                .WithDescription("Command or subcommand")
                .WithType(ApplicationCommandOptionType.String)
                .WithRequired(required)
                .WithMinLength(1)
                .WithMaxLength(96)
                .WithAutocomplete(true);
        }

        SlashCommandOptionBuilder CloneRoleOption()
        {
            return new SlashCommandOptionBuilder()
                .WithName(roleOption.Name)
                .WithDescription(roleOption.Description)
                .WithType(roleOption.Type)
                .WithRequired(roleOption.IsRequired ?? false);
        }
    }

    public async Task HandleAsync(SocketSlashCommand command)
    {
        var subCommand = command.Data.Options.FirstOrDefault();
        if (subCommand == null)
        {
            await command.RespondAsync(replies.Format(ReplyKind.Denied, "Unknown roles command."), ephemeral: true);
            return;
        }

        switch (subCommand.Name)
        {
            case "add":
                await HandleAddAsync(command, subCommand);
                return;
            case "remove":
                await HandleRemoveAsync(command, subCommand);
                return;
            case "list":
                await HandleListAsync(command, subCommand);
                return;
            default:
                await command.RespondAsync(replies.Format(ReplyKind.Denied, "Unknown roles command."), ephemeral: true);
                return;
        }
    }

    public async Task<IReadOnlyCollection<AutocompleteResult>> GetAutocompleteAsync(SocketAutocompleteInteraction interaction)
    {
        if (interaction.Data.Current.Name != "command")
            return [];

        var query = interaction.Data.Current.Value?.ToString();
        var accessKeys = await GetAccessKeysAsync(CancellationToken.None);
        return accessKeys
            .Where(key => Matches(key, query))
            .Take(25)
            .Select(key => new AutocompleteResult($"/{key}", key))
            .ToArray();
    }

    private async Task HandleAddAsync(SocketSlashCommand command, SocketSlashCommandDataOption subCommand)
    {
        if (!TryReadCommandName(subCommand, out var commandName))
        {
            await command.RespondAsync(replies.Format(ReplyKind.Empty, "Unknown command."), ephemeral: true);
            return;
        }

        var role = ReadRole(subCommand);
        var added = await roleAccessStore.AddRoleAsync(commandName, role.Id, CancellationToken.None);
        var details = added
            ? $"Added {role.Mention} to `/{commandName}`."
            : $"{role.Mention} is already allowed for `/{commandName}`.";

        await command.RespondAsync(replies.Format(ReplyKind.Success, details), ephemeral: true);
    }

    private async Task HandleRemoveAsync(SocketSlashCommand command, SocketSlashCommandDataOption subCommand)
    {
        if (!TryReadCommandName(subCommand, out var commandName))
        {
            await command.RespondAsync(replies.Format(ReplyKind.Empty, "Unknown command."), ephemeral: true);
            return;
        }

        var role = ReadRole(subCommand);
        var removed = await roleAccessStore.RemoveRoleAsync(commandName, role.Id, CancellationToken.None);
        var details = removed
            ? $"Removed {role.Mention} from `/{commandName}`."
            : $"{role.Mention} was not allowed for `/{commandName}`.";

        await command.RespondAsync(replies.Format(removed ? ReplyKind.Success : ReplyKind.Empty, details), ephemeral: true);
    }

    private async Task HandleListAsync(SocketSlashCommand command, SocketSlashCommandDataOption subCommand)
    {
        var commandOption = subCommand.Options.FirstOrDefault(option => option.Name == "command");
        if (commandOption != null)
        {
            if (!TryReadCommandName(subCommand, out var commandName))
            {
                await command.RespondAsync(replies.Format(ReplyKind.Empty, "Unknown command."), ephemeral: true);
                return;
            }

            var roleIds = await accessService.GetEffectiveRoleIdsAsync(commandName, CancellationToken.None);
            var commandDetails = roleIds.Count == 0
                ? $"`/{commandName}`: none"
                : $"`/{commandName}`:\n{FormatRoleIds(roleIds)}";

            await command.RespondAsync(replies.Format(roleIds.Count == 0 ? ReplyKind.Empty : ReplyKind.Success, commandDetails), ephemeral: true);
            return;
        }

        var lines = new List<string>();
        var accessKeys = await GetAccessKeysAsync(CancellationToken.None);
        foreach (var commandName in accessKeys)
        {
            var roleIds = await accessService.GetEffectiveRoleIdsAsync(commandName, CancellationToken.None);
            var roles = roleIds.Count == 0
                ? "none"
                : string.Join(", ", roleIds.Select(roleId => $"<@&{roleId}> (`{roleId}`)"));

            lines.Add($"`/{commandName}`: {roles}");
        }

        var allDetails = string.Join("\n", lines);
        await command.RespondAsync(replies.Format(ReplyKind.Success, allDetails), ephemeral: true);
    }

    private static bool TryReadCommandName(SocketSlashCommandDataOption subCommand, out string commandName)
    {
        var option = subCommand.Options.FirstOrDefault(option => option.Name == "command");
        if (option?.Value is string raw && SlashCommandNames.TryNormalizeAccessKey(raw, out commandName))
            return true;

        commandName = string.Empty;
        return false;
    }

    private async Task<IReadOnlyList<string>> GetAccessKeysAsync(CancellationToken cancellationToken)
    {
        var stored = await roleAccessStore.GetCommandRoleIdsAsync(cancellationToken);
        return SlashCommandNames.AllAccessKeys
            .Concat(stored.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool Matches(string value, string? query)
    {
        return string.IsNullOrWhiteSpace(query) ||
            value.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatRoleIds(IEnumerable<ulong> roleIds)
    {
        return string.Join("\n", roleIds.Select(roleId => $"<@&{roleId}> (`{roleId}`)"));
    }

    private static IRole ReadRole(SocketSlashCommandDataOption subCommand)
    {
        var option = subCommand.Options.FirstOrDefault(option => option.Name == "role")
            ?? throw new InvalidOperationException("Role option is missing.");

        return option.Value as IRole
            ?? throw new InvalidOperationException("Role option is invalid.");
    }
}
