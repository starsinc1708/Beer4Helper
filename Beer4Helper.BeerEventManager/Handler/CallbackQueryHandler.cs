using Beer4Helper.BeerEventManager.Models;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Beer4Helper.BeerEventManager.Handler;

public class CallbackQueryHandler(
    ITelegramBotClient botClient,
    PollMakerDbContext dbContext,
    ILogger<CallbackQueryHandler> logger)
{
    public async Task ProcessUpdate(Update update, CancellationToken ct)
    {
        var callbackQuery =  update.CallbackQuery!;
        var user = callbackQuery.From;
        var message = callbackQuery.Message!;
        
        var lastPoll = await dbContext.Polls.OrderByDescending(p => p.CreatedAt).FirstOrDefaultAsync(ct);
        
        if (lastPoll is null) return;
        if (lastPoll.MessageId != message.MessageId) return;
        
        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        
        try
        {
            var poll = await dbContext.Polls.Where(p => p.MessageId == message.MessageId).FirstOrDefaultAsync(ct);
            if (poll is null)
            {
                await transaction.RollbackAsync(ct);
                return;
            }
            
            poll.TotalVotes += 1;

            var pollOption = await dbContext.PollOptions
                .Where(po => po.PollId == poll.Id && po.Id == Guid.Parse(callbackQuery.Data!))
                .FirstOrDefaultAsync(cancellationToken: ct);
            pollOption!.VotesCount += 1;

            var newVote = new UserVote
            {
                PollId = poll.Id,
                PollOptionId = pollOption.Id,
                UserId = user.Id
            };

            await dbContext.UserVotes.AddAsync(newVote, ct);
            await dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(ct);
        }
    }
}