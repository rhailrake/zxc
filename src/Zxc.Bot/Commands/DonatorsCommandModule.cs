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
    IReplyService replies) : ISlashCommandModule
{
    private const int DiscordEmbedDescriptionLimit = 4096;
    private const int MaxConcurrentLookups = 12;

    public string Name => SlashCommandNames.Donators;

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

        var embed = BuildDonatorEmbed(lines);
        await command.ModifyOriginalResponseAsync(message =>
        {
            message.Content = replies.Pick(ReplyKind.Success);
            message.Embed = embed;
        });
    }

    private async Task<List<string>> BuildDonatorLinesAsync(SocketGuild guild, IReadOnlyCollection<ulong> roleIds)
    {
        var entries = new List<DonatorListEntry>();
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

                entries.Add(new DonatorListEntry(role, member));
            }
        }

        var ckeys = await FetchCkeysAsync(entries.Select(entry => entry.Member.Id).Distinct().ToArray());

        return entries
            .Select(entry => $"{entry.Role.Mention}: {entry.Member.Mention} - {ckeys[entry.Member.Id]}")
            .ToList();
    }

    private async Task<Dictionary<ulong, string>> FetchCkeysAsync(IReadOnlyCollection<ulong> userIds)
    {
        using var semaphore = new SemaphoreSlim(MaxConcurrentLookups);
        var tasks = userIds.Select(async userId =>
        {
            await semaphore.WaitAsync();
            try
            {
                var lookup = await lookupService.FindByDiscordIdAsync(userId.ToString(), CancellationToken.None);
                var ckey = lookup.Player?.PlayerUserName;
                if (string.IsNullOrWhiteSpace(ckey))
                    ckey = "unknown";

                return new KeyValuePair<ulong, string>(userId, ckey);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        return results.ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    private static Embed BuildDonatorEmbed(IReadOnlyCollection<string> lines)
    {
        var (description, hiddenCount) = BuildEmbedDescription(lines);
        var builder = new EmbedBuilder()
            .WithTitle("Donators")
            .WithDescription(description)
            .WithColor(new Color(255, 196, 0))
            .WithCurrentTimestamp();

        if (hiddenCount > 0)
            builder.WithFooter($"+{hiddenCount} more");

        return builder.Build();
    }

    private static (string Description, int HiddenCount) BuildEmbedDescription(IReadOnlyCollection<string> lines)
    {
        var builder = new StringBuilder();
        var includedCount = 0;

        foreach (var line in lines)
        {
            var lineValue = line.Length > DiscordEmbedDescriptionLimit
                ? line[..(DiscordEmbedDescriptionLimit - 3)] + "..."
                : line;

            var separatorLength = builder.Length == 0 ? 0 : Environment.NewLine.Length;
            if (builder.Length + separatorLength + lineValue.Length > DiscordEmbedDescriptionLimit)
                break;

            if (builder.Length > 0)
                builder.AppendLine();

            builder.Append(lineValue);
            includedCount++;
        }

        return (builder.ToString(), lines.Count - includedCount);
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

    private sealed record DonatorListEntry(
        SocketRole Role,
        SocketGuildUser Member);
}
