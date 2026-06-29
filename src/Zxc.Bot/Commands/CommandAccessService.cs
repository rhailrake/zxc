using Discord;
using Discord.WebSocket;
using Zxc.Bot.Access;
using Zxc.Bot.Configuration;

namespace Zxc.Bot.Commands;

public sealed class CommandAccessService(
    IRoleAccessStore roleAccessStore,
    DiscordOptions discordOptions)
{
    public async Task<bool> CanUseAsync(IUser user, string commandName, CancellationToken cancellationToken)
    {
        if (user is not SocketGuildUser guildUser)
            return false;

        var allowedRoleIds = await GetEffectiveRoleIdsAsync(commandName, cancellationToken);
        if (allowedRoleIds.Count == 0)
            return false;

        return guildUser.Roles.Any(role => allowedRoleIds.Contains(role.Id));
    }

    public async Task<bool> CanManageAccessAsync(IUser user, string commandName, CancellationToken cancellationToken)
    {
        if (user is not SocketGuildUser guildUser)
            return false;

        if (guildUser.GuildPermissions.Administrator ||
            guildUser.GuildPermissions.ManageGuild ||
            guildUser.GuildPermissions.ManageRoles)
            return true;

        return await CanUseAsync(user, commandName, cancellationToken);
    }

    public async Task<IReadOnlyCollection<ulong>> GetEffectiveRoleIdsAsync(string commandName, CancellationToken cancellationToken)
    {
        var roleIds = await roleAccessStore.GetRoleIdsAsync(commandName, cancellationToken);

        if (!string.Equals(commandName, SlashCommandNames.Roles, StringComparison.OrdinalIgnoreCase))
            return roleIds;

        return roleIds
            .Concat(discordOptions.AllowedRoleIds)
            .Distinct()
            .Order()
            .ToArray();
    }
}
