using Beer4Helper.PollingService.Polling;
using Beer4Helper.PollingService.Services;
using Beer4Helper.Shared;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);

var botModules = ConfigLoader.LoadConfig("bot-settings.yml");
//var botModules = ConfigLoader.LoadConfig("../bot-settings.dev.yml");
builder.Services.AddSingleton(botModules);

builder.Services.AddScoped<UpdateDistributor>();
builder.Services.AddScoped<ModuleService>();

builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(botModules.Token ?? string.Empty));

builder.Services.AddHostedService<TelegramPollingService>();
builder.Services.AddHttpClient();

var app = builder.Build();

app.Run();