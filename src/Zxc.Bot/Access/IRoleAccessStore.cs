namespace Zxc.Bot.Access;

public interface IRoleAccessStore
{
    Task<IReadOnlyCollection<ulong>> GetAllowedRoleIdsAsync(CancellationToken cancellationToken);

    Task<bool> AddRoleAsync(ulong roleId, CancellationToken cancellationToken);

    Task<bool> RemoveRoleAsync(ulong roleId, CancellationToken cancellationToken);
}
