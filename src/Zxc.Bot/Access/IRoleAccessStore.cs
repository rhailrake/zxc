namespace Zxc.Bot.Access;

public interface IRoleAccessStore
{
    Task<IReadOnlyDictionary<string, IReadOnlyCollection<ulong>>> GetCommandRoleIdsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ulong>> GetRoleIdsAsync(string commandName, CancellationToken cancellationToken);

    Task<bool> AddRoleAsync(string commandName, ulong roleId, CancellationToken cancellationToken);

    Task<bool> RemoveRoleAsync(string commandName, ulong roleId, CancellationToken cancellationToken);
}
