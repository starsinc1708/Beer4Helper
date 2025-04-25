using Beer4Helper.BeerEventManager.Handler;
using Beer4Helper.BeerEventManager.Models.Dto;
using Beer4Helper.Shared;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Poll = Beer4Helper.BeerEventManager.Models.Poll;
using PollOption = Beer4Helper.BeerEventManager.Models.PollOption;

namespace Beer4Helper.BeerEventManager;
public class PollMakerBotService(
    ITelegramBotClient botClient,
    TgBotSettings botSettings,
    ILogger<PollMakerBotService> logger,
    CallbackQueryHandler callbackQueryHandler,
    PollMakerDbContext dbContext)
{
    public async Task HandleUpdate(TgUpdateRequest? request, CancellationToken ct)
    {
        if  (request == null) return;

        var update = request.Update!;
        
        if (request.Type!.Equals(UpdateType.Message.ToString()))
        {
            logger.LogInformation($"Received message: {update.Message!.Text}");
        }
        else if (request.Type!.Equals(UpdateType.Poll.ToString()))
        {
            logger.LogInformation($"Received Poll reaction: {update.Poll!.Options.Select(opt => opt.Text).Aggregate((a, b) => $"{a}, {b}")}");
        }
        else if (request.Type!.Equals(UpdateType.PollAnswer.ToString()))
        {
            logger.LogInformation($"Received Poll reaction: {update.PollAnswer!.User!.Username}");
        }
        else if  (request.Type!.Equals(UpdateType.CallbackQuery.ToString()))
        {
            await callbackQueryHandler.ProcessUpdate(update, ct);
        }
    }

    public async Task CreateNewPoll(CancellationToken ct)
    {
        var chatIds = botSettings.BotModules!
            .FirstOrDefault(m => m.Key.Equals("BeerEventManager"))
            .Value.ParsedAllowedChats!.FirstOrDefault(chats => chats.Key.Equals(UpdateSource.Channel)).Value;
        logger.LogInformation($"Creating new poll for {string.Join(',', chatIds)}");

        var chatId = long.Parse(chatIds.FirstOrDefault()!);
        var pollDto = FormPoll(chatId, ct);
        var inlineKeyboard = CreatePollOptionKeyboard(pollDto);
        var sentMsg = await botClient.SendMessage(chatId, pollDto.MessageText!, ParseMode.Html, replyMarkup: inlineKeyboard, cancellationToken: ct);
        
        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);

        try
        {
            var poll = new Poll
            {
                ChatId = chatId,
                MessageId = sentMsg.MessageId,
                MessageText = pollDto.MessageText,
                TotalVotes = 0,
                CreatedAt = DateTime.UtcNow,
                Duration = TimeSpan.FromHours(4),
                ClosedAt = default
            };

            var dbPoll = await dbContext.Polls.AddAsync(poll, ct);

            await dbContext.SaveChangesAsync(ct);

            var pollOptions = pollDto.Options!.Select(opt => new PollOption
            {
                Id = opt.Id,
                PollId = dbPoll.Entity.Id,
                Text = opt.Text,
                VotesCount = 0
            });

            await dbContext.PollOptions.AddRangeAsync(pollOptions, ct);
            await dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(ct);
        }
    }

    private InlineKeyboardMarkup CreatePollOptionKeyboard(PollDto pollOptions)
    {
        var keyboard = new InlineKeyboardMarkup();
        var options = pollOptions.Options!;
    
        for (var i = 0; i < options.Count; i += 2)
        {
            if (i + 1 < options.Count)
            {
                keyboard.AddNewRow(
                    InlineKeyboardButton.WithCallbackData(options[i].Text!, options[i].Id.ToString()),
                    InlineKeyboardButton.WithCallbackData(options[i+1].Text!, options[i+1].Id.ToString())
                );
            }
            else
            {
                keyboard.AddNewRow(
                    InlineKeyboardButton.WithCallbackData(options[i].Text!, options[i].Id.ToString())
                );
            }
        }
    
        return keyboard;
    }

    private PollDto FormPoll(long chatId, CancellationToken ct)
    {
        var result = new PollDto
        {
            ChatId = chatId,
            MessageText = "КуДа ИдЁм ?",
            Options =
            [
                new PollOptionDto
                {
                    Id = Guid.CreateVersion7(),
                    Text = "БФ"
                },
                new PollOptionDto()
                {
                    Id = Guid.CreateVersion7(),
                    Text = "Любочка"
                },
                new PollOptionDto
                {
                    Id = Guid.CreateVersion7(),
                    Text = "ВМ"
                },
                new PollOptionDto()
                {
                    Id = Guid.CreateVersion7(),
                    Text = "Посошок"
                },new PollOptionDto
                {
                    Id = Guid.CreateVersion7(),
                    Text = "8 бит"
                },
                new PollOptionDto()
                {
                    Id = Guid.CreateVersion7(),
                    Text = "Сезонка"
                }
            ]
        };

        return result;
    }

    public async Task UpdateLastPollVotes(CancellationToken ct)
    {
        var lastPoll = await dbContext.Polls.OrderByDescending(p => p.CreatedAt).FirstOrDefaultAsync(ct);
        
        var chatIds = botSettings.BotModules!
            .FirstOrDefault(m => m.Key.Equals("BeerEventManager"))
            .Value.ParsedAllowedChats!.FirstOrDefault(chats => chats.Key.Equals(UpdateSource.Channel)).Value;
        
        
    }
}