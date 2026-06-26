using Discord;
using Discord.WebSocket;

namespace Zxc.Bot.Commands;

public interface ISlashCommandModule
{
    string Name { get; }

    SlashCommandAccess Access { get; }

    SlashCommandProperties Build();

    Task HandleAsync(SocketSlashCommand command);
}
