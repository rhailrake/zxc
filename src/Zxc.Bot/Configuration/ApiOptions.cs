namespace Zxc.Bot.Configuration;

public sealed class ApiOptions
{
    public const string BaseUrlEnvironmentVariable = "ZXC_API_BASE_URL";
    public const string TokenEnvironmentVariable = "ZXC_API_TOKEN";
    public const string ApiKeyHeaderName = "API-KEY";

    public required Uri BaseUrl { get; init; }

    public required string Token { get; init; }

    public static ApiOptions FromEnvironment()
    {
        return new ApiOptions
        {
            BaseUrl = EnsureTrailingSlash(EnvironmentReader.ReadRequiredUri(BaseUrlEnvironmentVariable)),
            Token = EnvironmentReader.ReadRequired(TokenEnvironmentVariable),
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
