using Discord;
using Discord.WebSocket;

namespace Zxc.Bot.Commands;

public interface ISlashCommandAutocompleteModule : ISlashCommandModule
{
    Task<IReadOnlyCollection<AutocompleteResult>> GetAutocompleteAsync(SocketAutocompleteInteraction interaction);
}
