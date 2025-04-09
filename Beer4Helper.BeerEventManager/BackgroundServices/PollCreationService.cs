namespace Beer4Helper.BeerEventManager.BackgroundServices;

public class PollCreationService(
    ILogger<PollCreationService> logger,
    IServiceProvider services)
    : BackgroundService
{
    private DateTime _nextPollCreation;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            using var scope = services.CreateScope();
            var botService = scope.ServiceProvider.GetRequiredService<PollMakerBotService>();
            
            //_nextPollCreation = DateTime.UtcNow.AddMinutes(1);
            logger.LogInformation("Next poll creation scheduled for {NextUpdate}", _nextPollCreation + TimeSpan.FromHours(4));
            
            var pollCreationTask = DoPollCreationWork(botService, ct);
            
            await Task.WhenAll(pollCreationTask);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred in Telegram Bot service.");
            throw;
        }
    }
    
    private async Task DoPollCreationWork(PollMakerBotService botService, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                
                if (now >= _nextPollCreation)
                {
                    await botService.CreateNewPoll(stoppingToken);
                    
                    _nextPollCreation = DateTime.UtcNow.AddMinutes(1);
                    logger.LogInformation("Next stats update scheduled for {NextUpdate}", _nextPollCreation + TimeSpan.FromHours(4));
                }
                
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
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
}