namespace Beer4Helper.ReactionCounter.BackgroundServices;

public class ReactionStatUpdateService(
    ILogger<ReactionStatUpdateService> logger,
    IServiceProvider services)
    : BackgroundService
{
    private DateTime _nextStatsUpdate;
    private DateTime _nextTopMessagesUpdate;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            using var scope = services.CreateScope();
            var botService = scope.ServiceProvider.GetRequiredService<ReactionBotService>();
            
            _nextStatsUpdate = DateTime.UtcNow.AddMinutes(1);
            logger.LogInformation("Next stats update scheduled for {NextUpdate}", _nextStatsUpdate + TimeSpan.FromHours(4));
            _nextTopMessagesUpdate = DateTime.UtcNow.AddMinutes(1);
            logger.LogInformation("Next top messages update scheduled for {NextUpdate}", _nextTopMessagesUpdate + TimeSpan.FromHours(4));
            
            var statsUpdateTask = DoStatsUpdateWork(botService, ct);
            var editTopMessagesTask = DoEditTopMessagesWork(botService, ct);
            
            await Task.WhenAll(statsUpdateTask, editTopMessagesTask);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred in Telegram Bot service.");
            throw;
        }
    }
    
    private async Task DoStatsUpdateWork(ReactionBotService botService, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                
                if (now >= _nextStatsUpdate)
                {
                    await botService.UpdateUserStats(stoppingToken);
                    
                    _nextStatsUpdate = DateTime.UtcNow.AddMinutes(29);
                    logger.LogInformation("Next stats update scheduled for {NextUpdate}", _nextStatsUpdate + TimeSpan.FromHours(4));
                }
                
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
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
    
    private async Task DoEditTopMessagesWork(ReactionBotService botService, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                
                if (now >= _nextTopMessagesUpdate)
                {
                    await botService.UpdateAllTopMessages(stoppingToken);
                    
                    _nextTopMessagesUpdate = DateTime.UtcNow.AddMinutes(11);
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
                    _nextTopMessagesUpdate = DateTime.UtcNow.AddMinutes(11);
                    logger.LogError(ex, "Error in Editing top messages service");
                    logger.LogInformation("Next top messages update scheduled for {NextUpdate}", _nextTopMessagesUpdate + TimeSpan.FromHours(4));
                    await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
                }
            }
        }
    }
}