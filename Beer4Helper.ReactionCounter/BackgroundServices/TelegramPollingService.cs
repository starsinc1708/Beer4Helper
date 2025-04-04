using Beer4Helper.ReactionCounter.Handlers;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace Beer4Helper.ReactionCounter.BackgroundServices;

public class TelegramPollingService(
    IServiceProvider serviceProvider,
    ITelegramBotClient botClient,
    ILogger<TelegramPollingService> logger) : BackgroundService
{
    private readonly UpdateType[] AllowedUpdates =
    [
        UpdateType.Message,
        UpdateType.CallbackQuery,
        UpdateType.MyChatMember,
        UpdateType.MessageReaction,
        UpdateType.MessageReactionCount,
        UpdateType.Poll,
        UpdateType.PollAnswer,
        UpdateType.ChannelPost,
        UpdateType.EditedChannelPost,
    ];
    
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var distributor = scope.ServiceProvider.GetRequiredService<UpdateDistributor>();
            await DoPollingWork(distributor, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred in Telegram Polling service.");
            throw;
        }
    }

    private async Task DoPollingWork(UpdateDistributor distributor, CancellationToken ct)
    {
        var offset = 0;
        logger.LogInformation("Telegram Polling service is started...");
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var updates = await botClient.GetUpdates(
                    offset: offset, 
                    allowedUpdates: AllowedUpdates,
                    cancellationToken: ct);

                foreach (var update in updates)
                {
                    await distributor.DistributeUpdate(update, ct);
                    offset = update.Id + 1;
                }
                
                await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Telegram polling service is stopping...");
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while polling Telegram updates.");
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
            }
        }
    }
}