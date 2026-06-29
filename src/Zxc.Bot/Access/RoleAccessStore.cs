using System.Text.Json;
using Zxc.Bot.Configuration;

namespace Zxc.Bot.Access;

public sealed class RoleAccessStore(AccessOptions options) : IRoleAccessStore
{
    private const string LegacyAllowedRolesCommandName = "roles";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<IReadOnlyDictionary<string, IReadOnlyCollection<ulong>>> GetCommandRoleIdsAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var document = await ReadDocumentAsync(cancellationToken);
            return document.CommandRoleIds
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    pair => pair.Key,
                    pair => (IReadOnlyCollection<ulong>) pair.Value.Distinct().Order().ToArray(),
                    StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyCollection<ulong>> GetRoleIdsAsync(string commandName, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var document = await ReadDocumentAsync(cancellationToken);
            return document.CommandRoleIds.TryGetValue(NormalizeCommandName(commandName), out var roleIds)
                ? roleIds.Distinct().Order().ToArray()
                : [];
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> AddRoleAsync(string commandName, ulong roleId, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var document = await ReadDocumentAsync(cancellationToken);
            commandName = NormalizeCommandName(commandName);
            if (!document.CommandRoleIds.TryGetValue(commandName, out var roleIds))
            {
                roleIds = [];
                document.CommandRoleIds[commandName] = roleIds;
            }

            if (roleIds.Contains(roleId))
                return false;

            roleIds.Add(roleId);
            document.CommandRoleIds[commandName] = roleIds.Distinct().Order().ToList();
            await WriteDocumentAsync(document, cancellationToken);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> RemoveRoleAsync(string commandName, ulong roleId, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var document = await ReadDocumentAsync(cancellationToken);
            commandName = NormalizeCommandName(commandName);
            if (!document.CommandRoleIds.TryGetValue(commandName, out var roleIds) ||
                !roleIds.Remove(roleId))
            {
                return false;
            }

            if (roleIds.Count == 0)
                document.CommandRoleIds.Remove(commandName);
            else
                document.CommandRoleIds[commandName] = roleIds.Distinct().Order().ToList();

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

        var document = JsonSerializer.Deserialize<AccessStoreDocument>(json, JsonOptions) ?? new AccessStoreDocument();
        return NormalizeDocument(document);
    }

    private async Task WriteDocumentAsync(AccessStoreDocument document, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(options.StorePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        document = NormalizeDocument(document);
        document.AllowedRoleIds = [];

        var json = JsonSerializer.Serialize(document, JsonOptions);
        await File.WriteAllTextAsync(options.StorePath, json, cancellationToken);
    }

    private static AccessStoreDocument NormalizeDocument(AccessStoreDocument document)
    {
        var normalized = new Dictionary<string, List<ulong>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (commandName, roleIds) in document.CommandRoleIds)
        {
            var key = NormalizeCommandName(commandName);
            if (string.IsNullOrWhiteSpace(key))
                continue;

            normalized[key] = roleIds
                .Distinct()
                .Order()
                .ToList();
        }

        if (document.AllowedRoleIds.Count > 0)
        {
            if (!normalized.TryGetValue(LegacyAllowedRolesCommandName, out var roleIds))
            {
                roleIds = [];
                normalized[LegacyAllowedRolesCommandName] = roleIds;
            }

            roleIds.AddRange(document.AllowedRoleIds);
            normalized[LegacyAllowedRolesCommandName] = roleIds
                .Distinct()
                .Order()
                .ToList();
        }

        document.CommandRoleIds = normalized;
        return document;
    }

    private static string NormalizeCommandName(string commandName)
    {
        return commandName.Trim().ToLowerInvariant();
    }
}
