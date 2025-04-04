using System.Text;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Beer4Helper.ReactionCounter.Handlers;

public class UpdateDistributor(
    ReactionHandler reactionHandler,
    MessageHandler messageHandler,
    ILogger<UpdateDistributor> logger)
{
    public async Task DistributeUpdate(Update update, CancellationToken cancellationToken)
    {
        try
        {
            var updateLog = new StringBuilder("Update ");
            updateLog.Append('[' + update.Type + ']');
            updateLog.Append(" received");
            switch (update)
            {
                case { Type: UpdateType.MessageReaction, MessageReaction: not null }:
                    updateLog.Append($" from [{update.MessageReaction.Chat.Id}]");
                    await reactionHandler.ProcessUpdate(update, cancellationToken);
                    break;
                case { Type: UpdateType.Message, Message: not null }:
                    await messageHandler.ProcessUpdate(update, cancellationToken);
                    break;
                default:
                    logger.LogWarning("Unknown update type received.");
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing update.");
        }
    }
}