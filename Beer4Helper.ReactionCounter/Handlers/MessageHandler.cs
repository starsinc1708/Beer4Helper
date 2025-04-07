using Beer4Helper.ReactionCounter.Models;
using Beer4Helper.Shared;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Beer4Helper.ReactionCounter.Handlers;

public class MessageHandler(
    ITelegramBotClient botClient,
    ReactionDbContext dbContext,
    ILogger<ReactionBotService> logger)
{
    public async Task ProcessUpdate(Update update, CancellationToken cancellationToken)
    {
        var message = update.Message!;
        if (message.Chat.Type == ChatType.Private) return;
        await HandleMessage(message, cancellationToken);
    }

    private async Task HandleMessage(Message message, CancellationToken cancellationToken)
    {
        if (message.Photo is not null)
        {
            await SavePhotoMessage(message, cancellationToken);
            logger.LogInformation($"CHAT[{message.Chat.Id}] | PHOTO message SAVED | [from {message.From!.Username}]");
        }

        if (message.Entities?.Select(e => e.Type == MessageEntityType.BotCommand) != null)
        {
            var command = await HandleBotCommand(message, cancellationToken);
            logger.LogInformation($"CHAT[{message.Chat.Id}] | COMMAND [{command}] HANDLED | [from {message.From!.Username}]");
        }
    }

    private async Task SavePhotoMessage(Message message, CancellationToken cancellationToken)
    {
        var photo = message.Photo?.LastOrDefault();
        if (photo != null)
        {
            var photoMessage = new PhotoMessage
            {
                ChatId = message.Chat.Id,
                UserId = message.From?.Id ?? message.SenderChat!.Id,
                MessageId = message.MessageId,
                FileId = photo.FileId
            };
            
            await dbContext.PhotoMessages.AddAsync(photoMessage, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
    
    private async Task<string> HandleBotCommand(Message message, CancellationToken cancellationToken)
    {
        var chatId = message.Chat.Id;
        var textParts = message.Text?.Trim().Split("@")!;
        if (textParts.Length <= 1) return string.Empty;
        
        var command = textParts[0];
        var commandUsername = textParts[1].Split(' ')[0];
        
        var botUsername = (await botClient.GetMe(cancellationToken: cancellationToken)).Username;

        if (commandUsername != botUsername) return "command for other bot";
        
        var commandParts = textParts[1].Split(' ');
        var args = commandParts.Skip(1).ToArray();
        
        var period = DateTime.UtcNow.AddMonths(-1);
        var topCount = 10;
        var periodPrefix = 1;
        var periodPostfix = "m";
        
        /*foreach (var arg in args)
        {
            if (arg.StartsWith("top") && int.TryParse(arg[3..], out var count))
            {
                topCount = Math.Clamp(count, 1, 25);
            }
            else if (arg.EndsWith('d') && int.TryParse(arg[..^1], out var days))
            {
                period = DateTime.UtcNow.AddDays(-days);
                periodPrefix = days;
                periodPostfix = "d";
            }
            else if (arg.EndsWith('w') && int.TryParse(arg[..^1], out var weeks))
            {
                period = DateTime.UtcNow.AddDays(-weeks * 7);
                periodPrefix = weeks;
                periodPostfix = "w";
            }
            else if (arg.EndsWith('m') && int.TryParse(arg[..^1], out var months))
            {
                period = DateTime.UtcNow.AddMonths(-months);
                periodPrefix = months;
                periodPostfix = "m";
            }
        }
        
        switch (command)
        {
            case "/help":
                await botClient.SendMessage(chatId, 
                    "Команды:\n" +
                    $"/topusers@{botUsername} [period] [topX] - топ пользователей.\n" +
                    $"/topphotos@{botUsername} [period] [topX] - топ фото.\n" +
                    $"/topreactions@{botUsername} [period] [topX] - топ реакций.\n" +
                    $"/topinteractions@{botUsername} [period] [topX] - кто сколько реагировал.\n\n" +
                    "Параметры:\n" +
                    "period - период (1d, 2w, 3m)\n" +
                    "topX - количество элементов (top5, top10)",
                    parseMode: ParseMode.Html, cancellationToken: cancellationToken);
                break;
            case "/topusers":
                var topUsers = await GetTopUsersAsync(chatId, period, periodPrefix, periodPostfix, topCount, cancellationToken);
                await botClient.SendMessage(chatId, topUsers, parseMode: ParseMode.Html, cancellationToken: cancellationToken);
                break;
            case "/topinteractions":
                var topInteractions = await GetTopInteractionsAsync(chatId, period, periodPrefix, periodPostfix, topCount, cancellationToken);
                await botClient.SendMessage(chatId, topInteractions, parseMode: ParseMode.Html, cancellationToken: cancellationToken);
                break;
            case "/topphotos":
                var topPhotos = await GetTopPhotosAsync(chatId, period, periodPrefix, periodPostfix, topCount, cancellationToken);
                await botClient.SendMessage(chatId, topPhotos, parseMode: ParseMode.Html, cancellationToken: cancellationToken);
                break;
            case "/topreactions":
                var topReactions = await GetTopReactionsAsync(chatId, period, periodPrefix, periodPostfix, topCount, cancellationToken);
                await botClient.SendMessage(chatId, topReactions, parseMode: ParseMode.Html, cancellationToken: cancellationToken);
                break;
            default:
                logger.LogWarning($"Unknown command received: {command}");
                break;
        }*/
        return command;
    }
}