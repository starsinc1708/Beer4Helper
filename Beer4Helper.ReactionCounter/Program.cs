using Beer4Helper.ReactionCounter;
using Beer4Helper.ReactionCounter.BackgroundServices;
using Beer4Helper.ReactionCounter.Endpoints;
using Beer4Helper.ReactionCounter.Handlers;
using Beer4Helper.Shared;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ReactionDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PostgreSQL")));

var botModules = ConfigLoader.LoadConfig("bot-settings.yml");
builder.Services.AddSingleton(botModules);

builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(botModules.Token ?? string.Empty));

builder.Services.AddScoped<ReactionBotService>();
builder.Services.AddScoped<ReactionHandler>();
builder.Services.AddScoped<MessageHandler>();

builder.Services.AddHostedService<ReactionStatUpdateService>();

var app = builder.Build();

app.MapTelegramEndpoints();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ReactionDbContext>();
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