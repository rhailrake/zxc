namespace Zxc.Bot.Donators;

public interface IDonatorRoleStore
{
    Task<IReadOnlyCollection<ulong>> GetRoleIdsAsync(CancellationToken cancellationToken);

    Task<bool> AddRoleAsync(ulong roleId, CancellationToken cancellationToken);

    Task<bool> RemoveRoleAsync(ulong roleId, CancellationToken cancellationToken);
}
