namespace Beer4Helper.ReactionCounter;

public class TelegramHostedService(
    ILogger<TelegramHostedService> logger,
    IServiceScopeFactory serviceScopeFactory)
    : IHostedService
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Telegram Bot service is starting...");
        Task.Run(() => StartPollingUpdatesAsync(_cancellationTokenSource.Token), cancellationToken);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Telegram Bot service is stopping...");
        _cancellationTokenSource.Cancel();
        return Task.CompletedTask;
    }

    private async Task StartPollingUpdatesAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var botService = scope.ServiceProvider.GetRequiredService<TelegramBotService>();
        await PollUpdatesAsync(botService, cancellationToken);
    }

    private async Task PollUpdatesAsync(TelegramBotService botService, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var updates = await botService.GetUpdatesAsync(offset, cancellationToken);

                foreach (var update in updates)
                {
                    await botService.HandleUpdateAsync(update, cancellationToken);
                    offset = update.Id + 1;
                }
                await Task.Delay(500, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while polling Telegram updates.");
                await Task.Delay(500, cancellationToken);
            }
        }
    }
}