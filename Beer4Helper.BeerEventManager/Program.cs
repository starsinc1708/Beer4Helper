using Beer4Helper.BeerEventManager;
using Beer4Helper.BeerEventManager.BackgroundServices;
using Beer4Helper.BeerEventManager.Endpoints;
using Beer4Helper.BeerEventManager.Handler;
using Beer4Helper.Shared;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<PollMakerDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PostgreSQL")));

var botModules = ConfigLoader.LoadConfig("bot-settings.yml");
builder.Services.AddSingleton(botModules);

builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(botModules.Token ?? string.Empty));

builder.Services.AddScoped<PollMakerBotService>();
builder.Services.AddScoped<CallbackQueryHandler>();

builder.Services.AddHostedService<PollCreationService>();
builder.Services.AddHostedService<VotesUpdateService>();

var app = builder.Build();

app.MapTelegramEndpoints();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<PollMakerDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        await dbContext.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        logger.LogWarning($"An error occurred while applying migrations: {ex.Message}");
    }
}

app.Run();