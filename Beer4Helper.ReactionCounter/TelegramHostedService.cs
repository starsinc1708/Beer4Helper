namespace Beer4Helper.ReactionCounter;

public class TelegramHostedService(
    ILogger<TelegramHostedService> logger,
    IServiceProvider services)
    : BackgroundService
{
    private int _executionCount;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Telegram Bot service is starting...");

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