using Discord;
using Discord.WebSocket;
using Zxc.Bot.Access;
using Zxc.Bot.Configuration;

namespace Zxc.Bot.Commands;

public sealed class CommandAccessService(IRoleAccessStore roleAccessStore)
{
    public async Task<bool> CanUseAsync(IUser user, CancellationToken cancellationToken)
    {
        if (user is not SocketGuildUser guildUser)
            return false;

        var allowedRoleIds = await roleAccessStore.GetAllowedRoleIdsAsync(cancellationToken);
        if (allowedRoleIds.Count == 0)
            return false;

        return guildUser.Roles.Any(role => allowedRoleIds.Contains(role.Id));
    }

    public async Task<bool> CanManageAccessAsync(IUser user, CancellationToken cancellationToken)
    {
        if (user is not SocketGuildUser guildUser)
            return false;

        if (guildUser.GuildPermissions.Administrator ||
            guildUser.GuildPermissions.ManageGuild ||
            guildUser.GuildPermissions.ManageRoles)
            return true;

        return await CanUseAsync(user, cancellationToken);
    }
}
