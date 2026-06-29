using Discord;
using Discord.WebSocket;
using Zxc.Bot.Access;
using Zxc.Bot.Replies;

namespace Zxc.Bot.Commands;

public sealed class RoleCommandModule(
    IRoleAccessStore roleAccessStore,
    CommandAccessService accessService,
    IReplyService replies) : ISlashCommandModule
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

        var commandOption = new SlashCommandOptionBuilder()
            .WithName("command")
            .WithDescription("Command")
            .WithType(ApplicationCommandOptionType.String)
            .WithRequired(true);

        foreach (var commandName in SlashCommandNames.All)
            commandOption.AddChoice(commandName, commandName);

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
            var option = new SlashCommandOptionBuilder()
                .WithName(commandOption.Name)
                .WithDescription(commandOption.Description)
                .WithType(commandOption.Type)
                .WithRequired(required);

            foreach (var commandName in SlashCommandNames.All)
                option.AddChoice(commandName, commandName);

            return option;
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
        foreach (var commandName in SlashCommandNames.All)
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
        if (option?.Value is string raw && SlashCommandNames.TryNormalize(raw, out commandName))
            return true;

        commandName = string.Empty;
        return false;
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
