using Beer4Helper.ReactionCounter.Models;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot.Types;

namespace Beer4Helper.ReactionCounter.Handlers;

public class ReactionHandler(
    ReactionDbContext dbContext,
    ILogger<ReactionBotService> logger)
{
    public async Task ProcessUpdate(Update update, CancellationToken cancellationToken)
    {
        var reactionUpdate =  update.MessageReaction!;
        
        var newReactions = reactionUpdate.NewReaction;
        var oldReactions = reactionUpdate.OldReaction;
        var messageId = reactionUpdate.MessageId;
        var chatId = reactionUpdate.Chat.Id;
        var userId = reactionUpdate.User?.Id ?? reactionUpdate.ActorChat?.Id ?? -1;
        var username = reactionUpdate.User?.Username ?? string.Empty;

        foreach (var reaction in newReactions)
        {
            var emoji = ExtractReactionValue(reaction);
            if (oldReactions.Any(r => ExtractReactionValue(r) == emoji)) continue;
            await SaveReaction(chatId, userId, messageId, emoji, cancellationToken);
            logger.LogInformation($"CHAT[{reactionUpdate.Chat.Id}] | REACTION SAVED | [{emoji}] from [{username}] to message [{messageId}]");
        }
        
        foreach (var reaction in oldReactions)
        {
            var emoji = ExtractReactionValue(reaction);
            if (newReactions.Any(r => ExtractReactionValue(r) == emoji)) continue;
            await RemoveReaction(chatId, userId, messageId, emoji, cancellationToken);
            logger.LogInformation($"CHAT[{reactionUpdate.Chat.Id}]: REACTION REMOVED [{emoji}]  from [{username}] to message [{messageId}]");
        }

        return;

        static string ExtractReactionValue(ReactionType reaction)
        {
            return reaction switch
            {
                ReactionTypeEmoji emojiReaction => emojiReaction.Emoji,
                ReactionTypeCustomEmoji customEmojiReaction => customEmojiReaction.CustomEmojiId,
                _ => "Unknown"
            };
        }
    }
    
    private async Task SaveReaction(long chatId, long userId, long messageId, string emoji,
        CancellationToken cancellationToken)
    {
        var reaction = new Reaction
        {
            ChatId = chatId,
            UserId = userId,
            Emoji = emoji,
            MessageId = messageId,
            CreatedAt = DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow
        };

        await dbContext.Reactions.AddAsync(reaction, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
    
    private async Task RemoveReaction(long chatId, long userId, long messageId, string emoji,
        CancellationToken cancellationToken)
    {
        var reaction = await dbContext.Reactions
            .Where(r => r.ChatId == chatId && r.UserId == userId && r.MessageId == messageId && r.Emoji == emoji)
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);

        if (reaction != null)
        {
            dbContext.Reactions.Remove(reaction);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}