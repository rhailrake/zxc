namespace Zxc.Bot.GameServers;

public interface IGameServerStore
{
    Task<IReadOnlyCollection<GameServerRecord>> GetServersAsync(CancellationToken cancellationToken);

    Task<GameServerRecord?> GetServerAsync(string name, CancellationToken cancellationToken);

    Task<bool> AddOrUpdateServerAsync(GameServerRecord server, CancellationToken cancellationToken);

    Task<bool> RemoveServerAsync(string name, CancellationToken cancellationToken);
}
