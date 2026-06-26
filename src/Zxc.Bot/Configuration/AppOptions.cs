using Microsoft.Extensions.Logging;

namespace Zxc.Bot.Configuration;

public sealed class AppOptions
{
    public const string LogLevelEnvironmentVariable = "ZXC_LOG_LEVEL";

    public LogLevel LogLevel { get; init; } = LogLevel.Information;

    public required DiscordOptions Discord { get; init; }

    public required AccessOptions Access { get; init; }

    public required DonatorRoleOptions DonatorRoles { get; init; }

    public required AuthOptions Auth { get; init; }

    public required ApiOptions Api { get; init; }

    public required MaintenanceOptions Maintenance { get; init; }

    public static AppOptions FromEnvironment()
    {
        return new AppOptions
        {
            LogLevel = EnvironmentReader.ReadLogLevel(LogLevelEnvironmentVariable, LogLevel.Information),
            Discord = DiscordOptions.FromEnvironment(),
            Access = AccessOptions.FromEnvironment(),
            DonatorRoles = DonatorRoleOptions.FromEnvironment(),
            Auth = AuthOptions.FromEnvironment(),
            Api = ApiOptions.FromEnvironment(),
            Maintenance = MaintenanceOptions.FromEnvironment(),
        };
    }
}
