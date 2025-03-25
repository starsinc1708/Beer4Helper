namespace Beer4Helper.ReactionCounter;

public class TelegramHostedService(
    ILogger<TelegramHostedService> logger,
    IServiceScopeFactory serviceScopeFactory)
    : IHostedService
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Telegram Bot service is starting...");

        // Start the polling task in the background
        _ = Task.Run(() => StartPollingUpdatesAsync(_cancellationTokenSource.Token), cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Telegram Bot service is stopping...");
        _cancellationTokenSource.Cancel();
        return Task.CompletedTask;
    }

    private async Task StartPollingUpdatesAsync(CancellationToken cancellationToken)
    {
        // Create a scope to resolve scoped services inside the background task
        using (var scope = serviceScopeFactory.CreateScope())
        {
            var botService = scope.ServiceProvider.GetRequiredService<TelegramBotService>();
            await PollUpdatesAsync(botService, cancellationToken);
        }
    }

    private async Task PollUpdatesAsync(TelegramBotService botService, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                
                
                // Call the GetUpdatesAsync method to fetch new updates
                var updates = await botService.GetUpdatesAsync(offset, cancellationToken);

                foreach (var update in updates)
                {
                    // Handle each update
                    await botService.HandleUpdateAsync(update, cancellationToken);
                    offset = update.Id + 1; // Update the offset
                }

                // Delay between polls
                await Task.Delay(1000, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while polling Telegram updates.");
                await Task.Delay(1000, cancellationToken); // Retry after a delay
            }
        }
    }
}