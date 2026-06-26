namespace Zxc.Bot.Configuration;

public sealed class AuthOptions
{
    public const string BaseUrlEnvironmentVariable = "ZXC_AUTH_BASE_URL";

    public required Uri BaseUrl { get; init; }

    public static AuthOptions FromEnvironment()
    {
        return new AuthOptions
        {
            BaseUrl = EnsureTrailingSlash(EnvironmentReader.ReadUri(BaseUrlEnvironmentVariable, "https://auth.deadspace14.net/api/")),
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
