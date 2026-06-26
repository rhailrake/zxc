using Discord;
using Microsoft.Extensions.Logging;

namespace Zxc.Bot.Discord;

public sealed class DiscordLogForwarder(ILogger<DiscordLogForwarder> logger)
{
    public Task ForwardAsync(LogMessage message)
    {
        var text = message.Message;

        switch (message.Severity)
        {
            case LogSeverity.Critical:
            case LogSeverity.Error:
                logger.LogError(message.Exception, "{Message}", text);
                break;
            case LogSeverity.Warning:
                logger.LogWarning(message.Exception, "{Message}", text);
                break;
            case LogSeverity.Info:
                logger.LogInformation(message.Exception, "{Message}", text);
                break;
            case LogSeverity.Verbose:
            case LogSeverity.Debug:
                logger.LogDebug(message.Exception, "{Message}", text);
                break;
        }

        return Task.CompletedTask;
    }
}
