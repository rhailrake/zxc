using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Zxc.Bot.Commands;
using Zxc.Bot.Configuration;

namespace Zxc.Bot.Discord;

public sealed class DiscordBotHostedService(
    DiscordOptions options,
    AiOptions aiOptions,
    DiscordSocketClient client,
    DiscordLogForwarder logForwarder,
    SlashCommandRegistrar commandRegistrar,
    SlashCommandDispatcher commandDispatcher,
    AiMentionResponder aiMentionResponder,
    ILogger<DiscordBotHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("AI mention responder is {State}. Model: {Model}.", aiOptions.Enabled ? "enabled" : "disabled", aiOptions.Model);

        client.Log += logForwarder.ForwardAsync;
        client.Ready += OnReadyAsync;
        client.SlashCommandExecuted += commandDispatcher.HandleAsync;
        client.AutocompleteExecuted += commandDispatcher.HandleAutocompleteAsync;
        client.MessageReceived += aiMentionResponder.HandleAsync;

        await client.LoginAsync(TokenType.Bot, options.Token);
        await client.StartAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        client.SlashCommandExecuted -= commandDispatcher.HandleAsync;
        client.AutocompleteExecuted -= commandDispatcher.HandleAutocompleteAsync;
        client.MessageReceived -= aiMentionResponder.HandleAsync;
        client.Ready -= OnReadyAsync;
        client.Log -= logForwarder.ForwardAsync;

        try
        {
            await client.StopAsync();
            await client.LogoutAsync();
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failed to stop Discord client cleanly.");
        }
    }

    private async Task OnReadyAsync()
    {
        logger.LogInformation("Logged in as {Username} ({UserId}).", client.CurrentUser.Username, client.CurrentUser.Id);
        await client.SetStatusAsync(UserStatus.Invisible);
        await commandRegistrar.RegisterAsync();
    }
}
