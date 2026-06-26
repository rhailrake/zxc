namespace Zxc.Bot.Ai;

public interface IAiChatClient
{
    Task<string?> CreateReplyAsync(string prompt, CancellationToken cancellationToken);
}
