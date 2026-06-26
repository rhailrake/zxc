using System.Text;
using Discord;
using Discord.WebSocket;
using Zxc.Bot.Donators;
using Zxc.Bot.Players;
using Zxc.Bot.Replies;

namespace Zxc.Bot.Commands;

public sealed class DonatorsCommandModule(
    IDonatorRoleStore donatorRoleStore,
    IPlayerDiscordLookupService lookupService,
    CommandAccessService accessService,
    IReplyService replies) : ISlashCommandModule
{
    private const int DiscordMessageLimit = 1900;

    public string Name => "donators";

    public SlashCommandAccess Access => SlashCommandAccess.Role;

    public SlashCommandProperties Build()
    {
        var roleIdOption = new SlashCommandOptionBuilder()
            .WithName("role_id")
            .WithDescription("Discord role ID")
            .WithType(ApplicationCommandOptionType.String)
            .WithRequired(true)
            .WithMinLength(1)
            .WithMaxLength(32);

        var addRole = new SlashCommandOptionBuilder()
            .WithName("add-role")
            .WithDescription("Add donator role for fetch")
            .WithType(ApplicationCommandOptionType.SubCommand)
            .AddOption(CloneRoleIdOption());

        var removeRole = new SlashCommandOptionBuilder()
            .WithName("remove-role")
            .WithDescription("Remove donator role from fetch")
            .WithType(ApplicationCommandOptionType.SubCommand)
            .AddOption(CloneRoleIdOption());

        var roles = new SlashCommandOptionBuilder()
            .WithName("roles")
            .WithDescription("Show donator roles used for fetch")
            .WithType(ApplicationCommandOptionType.SubCommand);

        var list = new SlashCommandOptionBuilder()
            .WithName("list")
            .WithDescription("Show donators from configured roles")
            .WithType(ApplicationCommandOptionType.SubCommand);

        return new SlashCommandBuilder()
            .WithName(Name)
            .WithDescription("Donator role lookup")
            .AddOption(addRole)
            .AddOption(removeRole)
            .AddOption(roles)
            .AddOption(list)
            .Build();

        SlashCommandOptionBuilder CloneRoleIdOption()
        {
            return new SlashCommandOptionBuilder()
                .WithName(roleIdOption.Name)
                .WithDescription(roleIdOption.Description)
                .WithType(roleIdOption.Type)
                .WithRequired(roleIdOption.IsRequired ?? false)
                .WithMinLength(1)
                .WithMaxLength(32);
        }
    }

    public async Task HandleAsync(SocketSlashCommand command)
    {
        var subCommand = command.Data.Options.FirstOrDefault();
        if (subCommand == null)
        {
            await command.RespondAsync(replies.Format(ReplyKind.Denied), ephemeral: true);
            return;
        }

        switch (subCommand.Name)
        {
            case "add-role":
                await HandleAddRoleAsync(command, subCommand);
                return;
            case "remove-role":
                await HandleRemoveRoleAsync(command, subCommand);
                return;
            case "roles":
                await HandleRolesAsync(command);
                return;
            case "list":
                await HandleListAsync(command);
                return;
            default:
                await command.RespondAsync(replies.Format(ReplyKind.Denied), ephemeral: true);
                return;
        }
    }

    private async Task HandleAddRoleAsync(SocketSlashCommand command, SocketSlashCommandDataOption subCommand)
    {
        if (!await accessService.CanManageAccessAsync(command.User, CancellationToken.None))
        {
            await command.RespondAsync(replies.Format(ReplyKind.Denied), ephemeral: true);
            return;
        }

        var roleId = ReadRoleId(subCommand);
        if (command.Channel is not SocketGuildChannel guildChannel || guildChannel.Guild.GetRole(roleId) == null)
        {
            await command.RespondAsync(replies.Format(ReplyKind.Empty), ephemeral: true);
            return;
        }

        var added = await donatorRoleStore.AddRoleAsync(roleId, CancellationToken.None);
        var details = added
            ? $"Added <@&{roleId}>."
            : $"<@&{roleId}> is already configured.";

        await command.RespondAsync(replies.Format(ReplyKind.Success, details), ephemeral: true);
    }

    private async Task HandleRemoveRoleAsync(SocketSlashCommand command, SocketSlashCommandDataOption subCommand)
    {
        if (!await accessService.CanManageAccessAsync(command.User, CancellationToken.None))
        {
            await command.RespondAsync(replies.Format(ReplyKind.Denied), ephemeral: true);
            return;
        }

        var roleId = ReadRoleId(subCommand);
        var removed = await donatorRoleStore.RemoveRoleAsync(roleId, CancellationToken.None);
        var details = removed
            ? $"Removed <@&{roleId}>."
            : $"<@&{roleId}> was not configured.";

        await command.RespondAsync(replies.Format(removed ? ReplyKind.Success : ReplyKind.Empty, details), ephemeral: true);
    }

    private async Task HandleRolesAsync(SocketSlashCommand command)
    {
        var roleIds = await donatorRoleStore.GetRoleIdsAsync(CancellationToken.None);
        if (roleIds.Count == 0)
        {
            await command.RespondAsync(replies.Format(ReplyKind.Empty), ephemeral: true);
            return;
        }

        var details = string.Join("\n", roleIds.Select(roleId => $"<@&{roleId}> (`{roleId}`)"));
        await command.RespondAsync(replies.Format(ReplyKind.Success, details), ephemeral: true);
    }

    private async Task HandleListAsync(SocketSlashCommand command)
    {
        if (command.Channel is not SocketGuildChannel guildChannel)
        {
            await command.RespondAsync(replies.Format(ReplyKind.Denied), ephemeral: true);
            return;
        }

        var roleIds = await donatorRoleStore.GetRoleIdsAsync(CancellationToken.None);
        if (roleIds.Count == 0)
        {
            await command.RespondAsync(replies.Format(ReplyKind.Empty), ephemeral: true);
            return;
        }

        await command.DeferAsync();
        await guildChannel.Guild.DownloadUsersAsync();

        var lines = await BuildDonatorLinesAsync(guildChannel.Guild, roleIds);
        if (lines.Count == 0)
        {
            await command.ModifyOriginalResponseAsync(message => message.Content = replies.Format(ReplyKind.Empty));
            return;
        }

        var chunks = BuildChunks(lines);
        await command.ModifyOriginalResponseAsync(message => message.Content = replies.Format(ReplyKind.Success, chunks[0]));

        foreach (var chunk in chunks.Skip(1))
        {
            await command.FollowupAsync(chunk);
        }
    }

    private async Task<List<string>> BuildDonatorLinesAsync(SocketGuild guild, IReadOnlyCollection<ulong> roleIds)
    {
        var lines = new List<string>();
        var seen = new HashSet<(ulong RoleId, ulong UserId)>();

        foreach (var roleId in roleIds)
        {
            var role = guild.GetRole(roleId);
            if (role == null)
                continue;

            var members = role.Members.OrderBy(member => member.DisplayName, StringComparer.OrdinalIgnoreCase);
            foreach (var member in members)
            {
                if (!seen.Add((roleId, member.Id)))
                    continue;

                var lookup = await lookupService.FindByDiscordIdAsync(member.Id.ToString(), CancellationToken.None);
                var ckey = lookup.Player?.PlayerUserName;
                if (string.IsNullOrWhiteSpace(ckey))
                    ckey = "unknown";

                lines.Add($"{role.Mention}: {member.Mention} - {ckey}");
            }
        }

        return lines;
    }

    private static List<string> BuildChunks(IReadOnlyCollection<string> lines)
    {
        var chunks = new List<string>();
        var builder = new StringBuilder();

        foreach (var line in lines)
        {
            if (builder.Length + line.Length + 1 > DiscordMessageLimit && builder.Length > 0)
            {
                chunks.Add(builder.ToString().TrimEnd());
                builder.Clear();
            }

            builder.AppendLine(line);
        }

        if (builder.Length > 0)
            chunks.Add(builder.ToString().TrimEnd());

        return chunks;
    }

    private static ulong ReadRoleId(SocketSlashCommandDataOption subCommand)
    {
        var option = subCommand.Options.FirstOrDefault(option => option.Name == "role_id")
            ?? throw new InvalidOperationException("role_id option is missing.");

        var value = option.Value as string
            ?? throw new InvalidOperationException("role_id option is invalid.");

        if (!ulong.TryParse(value.Trim(), out var roleId))
            throw new InvalidOperationException("role_id option is invalid.");

        return roleId;
    }
}
