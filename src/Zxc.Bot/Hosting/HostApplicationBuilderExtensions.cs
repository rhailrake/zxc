using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Zxc.Bot.Access;
using Zxc.Bot.Ai;
using Zxc.Bot.Api;
using Zxc.Bot.Auth;
using Zxc.Bot.Commands;
using Zxc.Bot.Configuration;
using Zxc.Bot.Discord;
using Zxc.Bot.Donators;
using Zxc.Bot.Execution;
using Zxc.Bot.GameServers;
using Zxc.Bot.Players;
using Zxc.Bot.Replies;
using Zxc.Bot.SelfUpdate;

namespace Zxc.Bot.Hosting;

public static class HostApplicationBuilderExtensions
{
    public static void ConfigureZxcLogging(this HostApplicationBuilder builder, AppOptions options)
    {
        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(console =>
        {
            console.SingleLine = true;
            console.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
        });
        builder.Logging.SetMinimumLevel(options.LogLevel);
    }

    public static IServiceCollection AddZxcBot(this IServiceCollection services, AppOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton(options.Discord);
        services.AddSingleton(options.Access);
        services.AddSingleton(options.DonatorRoles);
        services.AddSingleton(options.GameServers);
        services.AddSingleton(options.Auth);
        services.AddSingleton(options.Api);
        services.AddSingleton(options.Ai);
        services.AddSingleton(options.Maintenance);

        services.AddHttpClient<IAuthApiClient, AuthApiClient>((provider, client) =>
        {
            var authOptions = provider.GetRequiredService<AuthOptions>();
            client.BaseAddress = authOptions.BaseUrl;
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        services.AddHttpClient<IDeadSpaceApiClient, DeadSpaceApiClient>((provider, client) =>
        {
            var apiOptions = provider.GetRequiredService<ApiOptions>();
            client.BaseAddress = apiOptions.BaseUrl;
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.Add(ApiOptions.ApiKeyHeaderName, apiOptions.Token);
        });

        services.AddHttpClient<IAiChatClient, AiChatClient>((provider, client) =>
        {
            var aiOptions = provider.GetRequiredService<AiOptions>();
            client.BaseAddress = aiOptions.BaseUrl;
            client.Timeout = aiOptions.Timeout;
            if (aiOptions.Enabled)
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", aiOptions.Token);
        });

        services.AddHttpClient<IGameServerApiClient, GameServerApiClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        services.AddSingleton<IReplyService, ReplyService>();
        services.AddSingleton<IRoleAccessStore, RoleAccessStore>();
        services.AddSingleton<IDonatorRoleStore, DonatorRoleStore>();
        services.AddSingleton<IGameServerStore, GameServerStore>();
        services.AddSingleton<IPlayerDiscordLookupService, PlayerDiscordLookupService>();
        services.AddSingleton<IProcessExecutor, ProcessExecutor>();
        services.AddSingleton<IBotMaintenanceService, BotMaintenanceService>();
        services.AddSingleton<CommandAccessService>();
        services.AddSingleton<ISlashCommandModule, AdminsCommandModule>();
        services.AddSingleton<ISlashCommandModule, BotCommandModule>();
        services.AddSingleton<ISlashCommandModule, RoleCommandModule>();
        services.AddSingleton<ISlashCommandModule, ServerCommandModule>();
        services.AddSingleton<ISlashCommandModule, DiscordLookupCommandModule>();
        services.AddSingleton<ISlashCommandModule, DonatorsCommandModule>();
        services.AddSingleton<SlashCommandDispatcher>();
        services.AddSingleton<SlashCommandRegistrar>();
        services.AddSingleton(DiscordClientFactory.Create());
        services.AddSingleton<DiscordLogForwarder>();
        services.AddSingleton<AiMentionResponder>();
        services.AddHostedService<DiscordBotHostedService>();

        return services;
    }
}
