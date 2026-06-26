using System.Text.Json;
using Zxc.Bot.Configuration;

namespace Zxc.Bot.Donators;

public sealed class DonatorRoleStore(DonatorRoleOptions options) : IDonatorRoleStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<IReadOnlyCollection<ulong>> GetRoleIdsAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var document = await ReadDocumentAsync(cancellationToken);
            return document.RoleIds.Distinct().Order().ToArray();
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
            if (document.RoleIds.Contains(roleId))
                return false;

            document.RoleIds.Add(roleId);
            document.RoleIds = document.RoleIds.Distinct().Order().ToList();
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
            if (!document.RoleIds.Remove(roleId))
                return false;

            document.RoleIds = document.RoleIds.Distinct().Order().ToList();
            await WriteDocumentAsync(document, cancellationToken);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<DonatorRoleStoreDocument> ReadDocumentAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(options.StorePath))
            return new DonatorRoleStoreDocument();

        var json = await File.ReadAllTextAsync(options.StorePath, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
            return new DonatorRoleStoreDocument();

        return JsonSerializer.Deserialize<DonatorRoleStoreDocument>(json, JsonOptions) ?? new DonatorRoleStoreDocument();
    }

    private async Task WriteDocumentAsync(DonatorRoleStoreDocument document, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(options.StorePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(document, JsonOptions);
        await File.WriteAllTextAsync(options.StorePath, json, cancellationToken);
    }
}
