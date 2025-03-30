using Google.Protobuf;
using Microsoft.Extensions.Options;

namespace Beer4Helper.ReactionCounter;

public class TelegramHostedService(
    ILogger<TelegramHostedService> logger,
    IOptions<TelegramBotSettings> settings,
    IServiceProvider services)
    : BackgroundService
{
    private readonly TelegramBotSettings _settings = settings.Value;
    
    private int _executionCount;
    private DateTime _nextTopUsersCheck;
    private const bool TestMode = false;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Telegram Bot service is starting...");
        _nextTopUsersCheck = TestMode ? CalculateNextMinuteTime() : CalculateNextMonthlyTopUsersTime();
        try
        {
            await DoWork(stoppingToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred in Telegram Bot service.");
            throw;
        }
    }

    private static DateTime CalculateNextMonthlyTopUsersTime()
    {
        var now = DateTime.UtcNow;
        var lastDayOfMonth = DateTime.DaysInMonth(now.Year, now.Month);

        if (now.Day != lastDayOfMonth || now.Hour < 12)
            return new DateTime(now.Year, now.Month, lastDayOfMonth, 12, 0, 0, DateTimeKind.Utc);
        
        now = now.AddMonths(1);
        lastDayOfMonth = DateTime.DaysInMonth(now.Year, now.Month);

        return new DateTime(now.Year, now.Month, lastDayOfMonth, 12, 0, 0, DateTimeKind.Utc);
    }

    private static DateTime CalculateNextMinuteTime()
    {
        return DateTime.UtcNow.AddMinutes(1);
    }

    private async Task CheckTopUsersAsync(TelegramBotService botService,
        CancellationToken stoppingToken)
    {
        var now = DateTime.UtcNow;
        if (now < _nextTopUsersCheck) return; 
        
        try
        {
            logger.LogInformation("It's time to get top users! Current UTC time: {CurrentTime}", now);
            logger.LogInformation(string.Join(',', _settings.ReactionChatIds));
            logger.LogInformation(string.Join(',', _settings.CommandChatIds));
            foreach (var chatId in _settings.ReactionChatIds.Distinct())
            {
                var topUserMsg = await botService.GetTopUsersAsync(chatId, DateTime.UtcNow.AddMonths(-1), 1, "m", 10, stoppingToken);
                await botService.SendMessage(chatId, topUserMsg, stoppingToken);
                
                var topPhotos = await botService.GetTopPhotosAsync(chatId, DateTime.UtcNow.AddMonths(-1), 1, "m", 10, stoppingToken);
                await botService.SendMessage(chatId, topPhotos, stoppingToken);
            }
                
            _nextTopUsersCheck = TestMode ? CalculateNextMinuteTime() : CalculateNextMonthlyTopUsersTime();
            logger.LogInformation("Next top users check scheduled for {NextCheck}", _nextTopUsersCheck);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while getting top users.");
        }
    }

    private async Task DoWork(CancellationToken stoppingToken)
    {
        var count = Interlocked.Increment(ref _executionCount);
        logger.LogInformation("Telegram Bot service is working. Count: {Count}", count);

        using var scope = services.CreateScope();
        var botService = scope.ServiceProvider.GetRequiredService<TelegramBotService>();
        var offset = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckTopUsersAsync(botService, stoppingToken);
                
                var updates = await botService.GetUpdatesAsync(offset, stoppingToken);

                foreach (var update in updates)
                {
                    await botService.HandleUpdateAsync(update, stoppingToken);
                    offset = update.Id + 1;
                }
                
                await Task.Delay(1000, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while polling Telegram updates.");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Telegram Bot service is stopping...");
        await base.StopAsync(stoppingToken);
    }
}