namespace Zxc.Bot.Configuration;

public sealed class MaintenanceOptions
{
    public const string UpdateScriptPathEnvironmentVariable = "ZXC_UPDATE_SCRIPT_PATH";
    public const string RestartScriptPathEnvironmentVariable = "ZXC_RESTART_SCRIPT_PATH";
    public const string RepositoryPathEnvironmentVariable = "ZXC_REPOSITORY_PATH";

    public required string UpdateScriptPath { get; init; }

    public required string RestartScriptPath { get; init; }

    public required string RepositoryPath { get; init; }

    public static MaintenanceOptions FromEnvironment()
    {
        return new MaintenanceOptions
        {
            UpdateScriptPath = EnvironmentReader.ReadString(UpdateScriptPathEnvironmentVariable, "/opt/zxc/update.sh"),
            RestartScriptPath = EnvironmentReader.ReadString(RestartScriptPathEnvironmentVariable, "/opt/zxc/restart.sh"),
            RepositoryPath = EnvironmentReader.ReadString(RepositoryPathEnvironmentVariable, "/opt/zxc-src"),
        };
    }
}
