using Microsoft.Extensions.Hosting;
using Zxc.Bot.Configuration;
using Zxc.Bot.Hosting;

var options = AppOptions.FromEnvironment();
var builder = Host.CreateApplicationBuilder(args);

builder.ConfigureZxcLogging(options);
builder.Services.AddZxcBot(options);

await builder.Build().RunAsync();
