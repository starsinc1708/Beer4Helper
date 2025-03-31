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
    private DateTime _nextStatsUpdate;
    private DateTime _nextTopMessagesUpdate;
    private const bool TestMode = false;
    private TelegramBotService? botService;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Telegram Bot service is starting...");
        
        using var scope = services.CreateScope();
        botService = scope.ServiceProvider.GetRequiredService<TelegramBotService>();

        try
        {
            var pollingTask = DoPollingWork(stoppingToken);
            var statsUpdateTask = DoStatsUpdateWork(stoppingToken);
            var editTopMessagesTask = DoEditTopMessagesWork(stoppingToken);
            
            await Task.WhenAll(pollingTask, statsUpdateTask, editTopMessagesTask);
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
            return new DateTime(now.Year, now.Month, lastDayOfMonth, 7, 0, 0, DateTimeKind.Utc);
        
        now = now.AddMonths(1);
        lastDayOfMonth = DateTime.DaysInMonth(now.Year, now.Month);

        return new DateTime(now.Year, now.Month, lastDayOfMonth, 7, 0, 0, DateTimeKind.Utc);
    }

    private async Task DoPollingWork(CancellationToken stoppingToken)
    {
        var count = Interlocked.Increment(ref _executionCount);
        logger.LogInformation("Telegram polling service is working. Count: {Count}", count);
        
        var offset = 0;
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var updates = await botService?.GetUpdatesAsync(offset, stoppingToken)!;

                foreach (var update in updates)
                {
                    await botService.HandleUpdateAsync(update, stoppingToken);
                    offset = update.Id + 1;
                }
                
                await Task.Delay(1000, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Telegram polling service is stopping...");
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while polling Telegram updates.");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }
    
    private async Task DoStatsUpdateWork(CancellationToken stoppingToken)
    {
        logger.LogInformation("User stats update service is starting...");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                if (now >= _nextStatsUpdate)
                {
                    await botService?.UpdateUserStats(stoppingToken)!;
                    _nextStatsUpdate = DateTime.UtcNow.AddHours(1);
                    logger.LogInformation("Next stats update scheduled for {NextUpdate}", _nextStatsUpdate + TimeSpan.FromHours(4));
                }
                
                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Stats update service is stopping...");
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in stats update service");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }
    
    private async Task DoEditTopMessagesWork(CancellationToken stoppingToken)
    {
        logger.LogInformation("Editing top messages is starting...");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                var timeRemaining = _nextTopMessagesUpdate - now;
                
                if (now >= _nextTopMessagesUpdate)
                {
                    await botService?.UpdateTopMessages(now.AddHours(4), stoppingToken)!;
                    _nextTopMessagesUpdate = DateTime.UtcNow.AddMinutes(10);
                    logger.LogInformation("Next top messages update scheduled for {NextUpdate}", _nextTopMessagesUpdate + TimeSpan.FromHours(4));
                }
                
                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Editing top messages service is stopping...");
                throw;
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("message is not modified"))
                {
                    _nextTopMessagesUpdate = DateTime.UtcNow.AddMinutes(10);
                    logger.LogError(ex, "Error in Editing top messages service");
                    logger.LogInformation("Next top messages update scheduled for {NextUpdate}", _nextTopMessagesUpdate + TimeSpan.FromHours(4));
                    await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
                }
            }
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Telegram Bot service is stopping...");
        await base.StopAsync(stoppingToken);
    }
}