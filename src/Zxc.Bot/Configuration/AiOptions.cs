namespace Zxc.Bot.Configuration;

public sealed class AiOptions
{
    public const string BaseUrlEnvironmentVariable = "ZXC_AI_BASE_URL";
    public const string TokenEnvironmentVariable = "ZXC_AI_TOKEN";
    public const string ModelEnvironmentVariable = "ZXC_AI_MODEL";
    public const string MaxTokensEnvironmentVariable = "ZXC_AI_MAX_TOKENS";
    public const string TemperatureEnvironmentVariable = "ZXC_AI_TEMPERATURE";
    public const string TimeoutSecondsEnvironmentVariable = "ZXC_AI_TIMEOUT_SECONDS";
    public const string MaxPromptCharsEnvironmentVariable = "ZXC_AI_MAX_PROMPT_CHARS";

    public required Uri BaseUrl { get; init; }

    public required string Token { get; init; }

    public required string Model { get; init; }

    public required int MaxTokens { get; init; }

    public required double Temperature { get; init; }

    public required TimeSpan Timeout { get; init; }

    public required int MaxPromptChars { get; init; }

    public bool Enabled => !string.IsNullOrWhiteSpace(Token);

    public static AiOptions FromEnvironment()
    {
        return new AiOptions
        {
            BaseUrl = EnsureTrailingSlash(EnvironmentReader.ReadUri(BaseUrlEnvironmentVariable, "https://integrate.api.nvidia.com/v1/")),
            Token = EnvironmentReader.ReadString(TokenEnvironmentVariable, string.Empty),
            Model = EnvironmentReader.ReadString(ModelEnvironmentVariable, "meta/llama-3.1-8b-instruct"),
            MaxTokens = EnvironmentReader.ReadInt(MaxTokensEnvironmentVariable, 240, 32, 1024),
            Temperature = EnvironmentReader.ReadDouble(TemperatureEnvironmentVariable, 0.72, 0, 2),
            Timeout = TimeSpan.FromSeconds(EnvironmentReader.ReadInt(TimeoutSecondsEnvironmentVariable, 45, 5, 300)),
            MaxPromptChars = EnvironmentReader.ReadInt(MaxPromptCharsEnvironmentVariable, 1200, 100, 4000),
        };
    }

    private static Uri EnsureTrailingSlash(Uri uri)
    {
        var value = uri.AbsoluteUri;
        return value.EndsWith("/", StringComparison.Ordinal)
            ? uri
            : new Uri(value + "/");
    }
}
