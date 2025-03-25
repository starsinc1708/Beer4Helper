using Beer4Helper.ReactionCounter;
using Beer4Helper.ReactionCounter.Data;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);

var settings = builder.Configuration.GetSection("TelegramBotSettings");
builder.Services.Configure<TelegramBotSettings>(settings);

// Log Telegram settings specifically
var telegramSettings = new TelegramBotSettings();
settings.Bind(telegramSettings);
Console.WriteLine($"Telegram Bot Settings:");
Console.WriteLine($"- Token: {builder.Configuration["TelegramBotToken"]}");
Console.WriteLine($"- AllowedChatIds: {string.Join(", ", telegramSettings.AllowedChatIds)}");
Console.WriteLine($"- ChatForCommandsId: {string.Join(", ", telegramSettings.CommandChatIds)}");

builder.Services.AddDbContext<ReactionDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PostgreSQL")));

builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(builder.Configuration["TelegramBotToken"] ?? string.Empty));

builder.Services.AddScoped<TelegramBotService>();
builder.Services.AddSingleton<IHostedService, TelegramHostedService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ReactionDbContext>();
    try
    {
        await dbContext.Database.MigrateAsync();
        Console.WriteLine("Migrations applied successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"An error occurred while applying migrations: {ex.Message}");
    }
}

app.Run();