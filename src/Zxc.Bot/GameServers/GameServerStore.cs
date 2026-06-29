using System.Text.Json;
using Zxc.Bot.Configuration;

namespace Zxc.Bot.GameServers;

public sealed class GameServerStore(GameServerOptions options) : IGameServerStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<IReadOnlyCollection<GameServerRecord>> GetServersAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var document = await ReadDocumentAsync(cancellationToken);
            return document.Servers
                .Select(ToRecord)
                .OrderBy(server => server.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<GameServerRecord?> GetServerAsync(string name, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            name = NormalizeName(name);
            var document = await ReadDocumentAsync(cancellationToken);
            var entry = document.Servers.FirstOrDefault(server => string.Equals(server.Name, name, StringComparison.OrdinalIgnoreCase));
            return entry == null ? null : ToRecord(entry);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> AddOrUpdateServerAsync(GameServerRecord server, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var document = await ReadDocumentAsync(cancellationToken);
            var name = NormalizeName(server.Name);
            var entry = document.Servers.FirstOrDefault(serverEntry => string.Equals(serverEntry.Name, name, StringComparison.OrdinalIgnoreCase));
            var added = entry == null;

            if (entry == null)
            {
                entry = new GameServerStoreEntry();
                document.Servers.Add(entry);
            }

            entry.Name = name;
            entry.Url = server.Url.AbsoluteUri;
            entry.Token = server.Token.Trim();

            await WriteDocumentAsync(document, cancellationToken);
            return added;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> RemoveServerAsync(string name, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            name = NormalizeName(name);
            var document = await ReadDocumentAsync(cancellationToken);
            var removed = document.Servers.RemoveAll(server => string.Equals(server.Name, name, StringComparison.OrdinalIgnoreCase)) > 0;
            if (!removed)
                return false;

            await WriteDocumentAsync(document, cancellationToken);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<GameServerStoreDocument> ReadDocumentAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(options.StorePath))
            return new GameServerStoreDocument();

        var json = await File.ReadAllTextAsync(options.StorePath, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
            return new GameServerStoreDocument();

        var document = JsonSerializer.Deserialize<GameServerStoreDocument>(json, JsonOptions) ?? new GameServerStoreDocument();
        return NormalizeDocument(document);
    }

    private async Task WriteDocumentAsync(GameServerStoreDocument document, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(options.StorePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        document = NormalizeDocument(document);

        var json = JsonSerializer.Serialize(document, JsonOptions);
        await File.WriteAllTextAsync(options.StorePath, json, cancellationToken);
    }

    private static GameServerStoreDocument NormalizeDocument(GameServerStoreDocument document)
    {
        var normalized = new Dictionary<string, GameServerStoreEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var server in document.Servers)
        {
            var name = NormalizeName(server.Name);
            if (string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(server.Token) ||
                !TryNormalizeUrl(server.Url, out var url))
            {
                continue;
            }

            normalized[name] = new GameServerStoreEntry
            {
                Name = name,
                Url = url.AbsoluteUri,
                Token = server.Token.Trim(),
            };
        }

        document.Servers = normalized.Values
            .OrderBy(server => server.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return document;
    }

    private static GameServerRecord ToRecord(GameServerStoreEntry entry)
    {
        return new GameServerRecord(
            entry.Name,
            new Uri(entry.Url, UriKind.Absolute),
            entry.Token);
    }

    private static bool TryNormalizeUrl(string value, out Uri url)
    {
        if (Uri.TryCreate(value.Trim(), UriKind.Absolute, out url!) &&
            (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps))
        {
            return true;
        }

        url = null!;
        return false;
    }

    private static string NormalizeName(string name)
    {
        return name.Trim().ToLowerInvariant();
    }
}
