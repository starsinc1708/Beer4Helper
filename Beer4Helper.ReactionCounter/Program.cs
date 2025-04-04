using Beer4Helper.ReactionCounter;
using Beer4Helper.ReactionCounter.BackgroundServices;
using Beer4Helper.ReactionCounter.ConfigModels;
using Beer4Helper.ReactionCounter.Endpoints;
using Beer4Helper.ReactionCounter.Handlers;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Polling;

var builder = WebApplication.CreateBuilder(args);

var settings = builder.Configuration.GetSection("TelegramBotSettings");
builder.Services.Configure<TelegramBotSettings>(settings);

var telegramSettings = new TelegramBotSettings();
settings.Bind(telegramSettings);

builder.Services.AddDbContext<ReactionDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PostgreSQL")));

builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(builder.Configuration["TelegramBotToken"] ?? string.Empty));

builder.Services.AddScoped<ReactionBotService>();
builder.Services.AddScoped<UpdateDistributor>();
builder.Services.AddScoped<ReactionHandler>();
builder.Services.AddScoped<MessageHandler>();

builder.Services.AddHostedService<TelegramPollingService>();
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