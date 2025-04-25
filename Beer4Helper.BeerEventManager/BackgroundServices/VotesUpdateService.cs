namespace Beer4Helper.BeerEventManager.BackgroundServices;

public class VotesUpdateService(
    ILogger<VotesUpdateService> logger,
    IServiceProvider services)
    : BackgroundService
{
    private DateTime _nextPollUpdate;
    
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            using var scope = services.CreateScope();
            var botService = scope.ServiceProvider.GetRequiredService<PollMakerBotService>();
            
            _nextPollUpdate = DateTime.UtcNow.AddMinutes(1);
            logger.LogInformation("Next poll update scheduled for {NextUpdate}", _nextPollUpdate + TimeSpan.FromHours(4));
            
            var pollUpdateTask = DoPollUpdateWork(botService, ct);
            
            await Task.WhenAll(pollUpdateTask);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred in Telegram Bot service.");
            throw;
        }
    }

    private async Task DoPollUpdateWork(PollMakerBotService botService, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                
                if (now >= _nextPollUpdate)
                {
                    await botService.UpdateLastPollVotes(ct);
                    
                    _nextPollUpdate = DateTime.UtcNow.AddMinutes(1);
                    logger.LogInformation("Next poll update scheduled for {NextUpdate}", _nextPollUpdate + TimeSpan.FromHours(4));
                }
                
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("poll update service is stopping...");
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in poll update service");
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
            }
        }
    }
}