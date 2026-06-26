using System.Text.Json;
using Zxc.Bot.Configuration;

namespace Zxc.Bot.Access;

public sealed class RoleAccessStore(
    AccessOptions options,
    DiscordOptions discordOptions) : IRoleAccessStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<IReadOnlyCollection<ulong>> GetAllowedRoleIdsAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var document = await ReadDocumentAsync(cancellationToken);
            return discordOptions.AllowedRoleIds
                .Concat(document.AllowedRoleIds)
                .Distinct()
                .Order()
                .ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> AddRoleAsync(ulong roleId, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var document = await ReadDocumentAsync(cancellationToken);
            if (document.AllowedRoleIds.Contains(roleId))
                return false;

            document.AllowedRoleIds.Add(roleId);
            document.AllowedRoleIds = document.AllowedRoleIds.Distinct().Order().ToList();
            await WriteDocumentAsync(document, cancellationToken);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> RemoveRoleAsync(ulong roleId, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var document = await ReadDocumentAsync(cancellationToken);
            if (!document.AllowedRoleIds.Remove(roleId))
                return false;

            document.AllowedRoleIds = document.AllowedRoleIds.Distinct().Order().ToList();
            await WriteDocumentAsync(document, cancellationToken);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<AccessStoreDocument> ReadDocumentAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(options.StorePath))
            return new AccessStoreDocument();

        var json = await File.ReadAllTextAsync(options.StorePath, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
            return new AccessStoreDocument();

        return JsonSerializer.Deserialize<AccessStoreDocument>(json, JsonOptions) ?? new AccessStoreDocument();
    }

    private async Task WriteDocumentAsync(AccessStoreDocument document, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(options.StorePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(document, JsonOptions);
        await File.WriteAllTextAsync(options.StorePath, json, cancellationToken);
    }
}
