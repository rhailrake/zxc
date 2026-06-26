using Discord;
using Discord.WebSocket;
using Zxc.Bot.Access;
using Zxc.Bot.Replies;

namespace Zxc.Bot.Commands;

public sealed class RoleCommandModule(
    IRoleAccessStore roleAccessStore,
    IReplyService replies) : ISlashCommandModule
{
    public string Name => "roles";

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
            .WithDescription("Allow role to use bot commands")
            .WithType(ApplicationCommandOptionType.SubCommand)
            .AddOption(CloneRoleOption());

        var remove = new SlashCommandOptionBuilder()
            .WithName("remove")
            .WithDescription("Remove role from bot access")
            .WithType(ApplicationCommandOptionType.SubCommand)
            .AddOption(CloneRoleOption());

        var list = new SlashCommandOptionBuilder()
            .WithName("list")
            .WithDescription("Show roles allowed to use bot commands")
            .WithType(ApplicationCommandOptionType.SubCommand);

        return new SlashCommandBuilder()
            .WithName(Name)
            .WithDescription("Bot access roles")
            .AddOption(add)
            .AddOption(remove)
            .AddOption(list)
            .Build();

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
                await HandleListAsync(command);
                return;
            default:
                await command.RespondAsync(replies.Format(ReplyKind.Denied, "Unknown roles command."), ephemeral: true);
                return;
        }
    }

    private async Task HandleAddAsync(SocketSlashCommand command, SocketSlashCommandDataOption subCommand)
    {
        var role = ReadRole(subCommand);
        var added = await roleAccessStore.AddRoleAsync(role.Id, CancellationToken.None);
        var details = added
            ? $"Added {role.Mention}."
            : $"{role.Mention} is already allowed.";

        await command.RespondAsync(replies.Format(ReplyKind.Success, details), ephemeral: true);
    }

    private async Task HandleRemoveAsync(SocketSlashCommand command, SocketSlashCommandDataOption subCommand)
    {
        var role = ReadRole(subCommand);
        var removed = await roleAccessStore.RemoveRoleAsync(role.Id, CancellationToken.None);
        var details = removed
            ? $"Removed {role.Mention}."
            : $"{role.Mention} was not in the stored role list.";

        await command.RespondAsync(replies.Format(removed ? ReplyKind.Success : ReplyKind.Empty, details), ephemeral: true);
    }

    private async Task HandleListAsync(SocketSlashCommand command)
    {
        var roleIds = await roleAccessStore.GetAllowedRoleIdsAsync(CancellationToken.None);
        if (roleIds.Count == 0)
        {
            await command.RespondAsync(replies.Format(ReplyKind.Empty), ephemeral: true);
            return;
        }

        var details = string.Join("\n", roleIds.Select(roleId => $"<@&{roleId}> (`{roleId}`)"));
        await command.RespondAsync(replies.Format(ReplyKind.Success, details), ephemeral: true);
    }

    private static IRole ReadRole(SocketSlashCommandDataOption subCommand)
    {
        var option = subCommand.Options.FirstOrDefault(option => option.Name == "role")
            ?? throw new InvalidOperationException("Role option is missing.");

        return option.Value as IRole
            ?? throw new InvalidOperationException("Role option is invalid.");
    }
}
