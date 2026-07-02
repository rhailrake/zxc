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

    public async Task<bool> CanManageAnyAsync(IUser user, string commandName, CancellationToken cancellationToken)
    {
        if (user is not SocketGuildUser guildUser)
            return false;

        if (guildUser.GuildPermissions.Administrator ||
            guildUser.GuildPermissions.ManageGuild ||
            guildUser.GuildPermissions.ManageRoles)
            return true;

        return await CanUseAnyAsync(user, commandName, cancellationToken);
    }

    public async Task<bool> CanUseAnyAsync(IUser user, string commandName, CancellationToken cancellationToken)
    {
        if (user is not SocketGuildUser guildUser)
            return false;

        if (await CanUseAsync(user, commandName, cancellationToken))
            return true;

        var userRoleIds = guildUser.Roles.Select(role => role.Id).ToHashSet();
        var commandPrefix = SlashCommandNames.BuildAccessKey(commandName) + " ";
        var commandRoleIds = await roleAccessStore.GetCommandRoleIdsAsync(cancellationToken);
        return commandRoleIds
            .Where(pair => pair.Key.StartsWith(commandPrefix, StringComparison.OrdinalIgnoreCase))
            .SelectMany(pair => pair.Value)
            .Any(userRoleIds.Contains);
    }

    public async Task<IReadOnlyCollection<ulong>> GetEffectiveRoleIdsAsync(string commandName, CancellationToken cancellationToken)
    {
        var roleIds = new List<ulong>();
        foreach (var accessKey in SlashCommandNames.GetParentAccessKeys(commandName))
        {
            roleIds.AddRange(await roleAccessStore.GetRoleIdsAsync(accessKey, cancellationToken));

            if (string.Equals(accessKey, SlashCommandNames.Roles, StringComparison.OrdinalIgnoreCase))
                roleIds.AddRange(discordOptions.AllowedRoleIds);
        }

        return roleIds
            .Distinct()
            .Order()
            .ToArray();
    }
}
