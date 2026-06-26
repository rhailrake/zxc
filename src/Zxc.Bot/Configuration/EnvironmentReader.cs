using Microsoft.Extensions.Logging;

namespace Zxc.Bot.Configuration;

public static class EnvironmentReader
{
    public static string ReadRequired(string variable)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"{variable} is not set.");

        return value.Trim();
    }

    public static string ReadString(string variable, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    public static Uri ReadRequiredUri(string variable)
    {
        var value = ReadRequired(variable);
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            throw new InvalidOperationException($"{variable} must be an absolute URL.");

        return uri;
    }

    public static Uri ReadUri(string variable, string fallback)
    {
        var value = ReadString(variable, fallback);
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            throw new InvalidOperationException($"{variable} must be an absolute URL.");

        return uri;
    }

    public static LogLevel ReadLogLevel(string variable, LogLevel fallback)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        return Enum.TryParse<LogLevel>(value, ignoreCase: true, out var level)
            ? level
            : fallback;
    }

    public static HashSet<ulong> ReadUlongSet(string variable)
    {
        var raw = Environment.GetEnvironmentVariable(variable);
        var result = new HashSet<ulong>();

        if (string.IsNullOrWhiteSpace(raw))
            return result;

        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!ulong.TryParse(part, out var value))
                throw new InvalidOperationException($"{variable} contains invalid ulong value: {part}");

            result.Add(value);
        }

        return result;
    }
}
