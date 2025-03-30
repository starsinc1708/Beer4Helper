using System.Text;
using Beer4Helper.ReactionCounter.Data;
using Beer4Helper.ReactionCounter.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Beer4Helper.ReactionCounter;

public class TelegramBotService(
    ITelegramBotClient botClient,
    IOptions<TelegramBotSettings> settings,
    ILogger<TelegramBotService> logger,
    ReactionDbContext dbContext)
{
    private readonly TelegramBotSettings _settings = settings.Value;
    
    public async Task<IEnumerable<Update>> GetUpdatesAsync(int offset, CancellationToken cancellationToken)
    {
        try
        {
            return await botClient.GetUpdates(offset: offset, cancellationToken: cancellationToken, allowedUpdates: [
                UpdateType.Message,
                UpdateType.CallbackQuery,
                UpdateType.MyChatMember,
                UpdateType.MessageReaction,
                UpdateType.MessageReactionCount
            ]);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting updates from Telegram.");
            return [];
        }
    }
    
    public async Task HandleUpdateAsync(Update update, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation($"Update received: {update.Type}");
            
            if (update.Type == UpdateType.MyChatMember)
            {
                logger.LogInformation($"My chat member update: {update.MyChatMember!.Chat.Id}");
            }
            
            switch (update)
            {
                case { Type: UpdateType.MessageReaction, MessageReaction: not null }:
                    if (_settings.ReactionChatIds.Contains(update.MessageReaction.Chat.Id))
                    {
                        await HandleReactionUpdate(update.MessageReaction, cancellationToken);
                    }
                    break;
                case { Type: UpdateType.Message, Message: not null }:
                    await HandleMessage(update.Message, cancellationToken);
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

    private async Task HandleReactionUpdate(MessageReactionUpdated reactionUpdate, CancellationToken cancellationToken)
    {
        var newReactions = reactionUpdate.NewReaction;
        var oldReactions = reactionUpdate.OldReaction;
        var messageId = reactionUpdate.MessageId;
        var chatId = reactionUpdate.Chat.Id;
        var userId = reactionUpdate.User?.Id ?? reactionUpdate.ActorChat?.Id ?? -1;
        var username = reactionUpdate.User?.Username ?? string.Empty;
        
        foreach (var reaction in newReactions)
        {
            var emoji = ExtractReactionValue(reaction);
            if (oldReactions.All(r => ExtractReactionValue(r) != emoji)) 
            {
                await SaveReaction(chatId, userId, username, messageId, emoji, cancellationToken);
            }
        }
        
        foreach (var reaction in oldReactions)
        {
            var emoji = ExtractReactionValue(reaction);
            if (newReactions.All(r => ExtractReactionValue(r) != emoji))
            {
                await RemoveReaction(chatId, userId, messageId, emoji, cancellationToken);
            }
        }
    }
    
    private static string ExtractReactionValue(ReactionType reaction)
    {
        return reaction switch
        {
            ReactionTypeEmoji emojiReaction => emojiReaction.Emoji,
            ReactionTypeCustomEmoji customEmojiReaction => customEmojiReaction.CustomEmojiId,
            _ => "Unknown"
        };
    }

    private async Task HandleMessage(Message message, CancellationToken cancellationToken)
    {
        if (message.Chat.Type == ChatType.Private) return;
        if (message.Photo != null)
        {
            if (_settings.ReactionChatIds.Contains(message.Chat.Id))
            {
                await SavePhotoMessage(message, cancellationToken);
            }
        }
        else if (message.Text != null)
        {
            if (_settings.CommandChatIds.Contains(message.Chat.Id))
            {
                await HandleCommand(message, cancellationToken);
            }
        }
    }

    private async Task SaveReaction(long chatId, long userId, string username, long messageId, string emoji,
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

        await UpdateUserStats(chatId, userId, username, messageId, cancellationToken);
    }
    
    private async Task UpdateUserStats(long chatId, long userId, string username, long messageId,
        CancellationToken cancellationToken)
    {
        var userStats = await dbContext.UserStats
            .Where(u => u.Id == userId)
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);

        if (userStats == null)
        {
            userStats = new UserStats
            {
                Id = userId,
                Username = username,
                TotalReactions = 1,
                TotalReactionsOnOwnMessages = 0,
                TotalReactionsOnOthersMessages = 1,
                TotalPhotosUploaded = 0
            };

            await dbContext.UserStats.AddAsync(userStats, cancellationToken);
        }
        else
        {
            userStats.TotalReactions++;
            
            var message = await dbContext.PhotoMessages
                .Where(m => m.ChatId == chatId && m.MessageId == messageId)
                .FirstOrDefaultAsync(cancellationToken: cancellationToken);

            if (message != null && message.UserId == userId)
            {
                userStats.TotalReactionsOnOwnMessages++;
            }
            else
            {
                userStats.TotalReactionsOnOthersMessages++;
            }
        }
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
            
            var userStats = await dbContext.UserStats
                .Where(u => u.Id == userId)
                .FirstOrDefaultAsync(cancellationToken: cancellationToken);

            if (userStats != null)
            {
                userStats.TotalReactions--;
                
                var message = await dbContext.PhotoMessages
                    .Where(m => m.ChatId == chatId && m.MessageId == messageId)
                    .FirstOrDefaultAsync(cancellationToken: cancellationToken);

                if (message != null && message.UserId == userId)
                {
                    userStats.TotalReactionsOnOwnMessages--;
                }
                else
                {
                    userStats.TotalReactionsOnOthersMessages--;
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
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
            
            
            var userStats = await dbContext.UserStats
                .Where(stat => stat.Id == message.From!.Id)
                .FirstOrDefaultAsync(cancellationToken: cancellationToken);
            
            if (userStats is null)
            {
                userStats = new UserStats
                {
                    Id = message.From!.Id,
                    Username = message.From.Username,
                    TotalReactions = 0,
                    TotalReactionsOnOwnMessages = 0,
                    TotalReactionsOnOthersMessages = 0,
                    TotalPhotosUploaded = 1
                };
                await dbContext.UserStats.AddAsync(userStats, cancellationToken);
            }
            else
            {
                userStats.TotalPhotosUploaded++;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
    
    private async Task HandleCommand(Message message, CancellationToken cancellationToken)
    {
        var chatId = message.Chat.Id;
        var textParts = message.Text?.Trim().Split("@")!;
        if (textParts.Length <= 1) return;
        
        var command = textParts[0];
        var commandUsername = textParts[1].Split(' ')[0];
        
        var botUsername = (await botClient.GetMe(cancellationToken: cancellationToken)).Username;

        if (commandUsername != botUsername) return;
        
        var commandParts = textParts[1].Split(' ');
        var args = commandParts.Skip(1).ToArray();
        
        var period = DateTime.UtcNow.AddMonths(-1);
        var topCount = 10;
        var periodPrefix = 1;
        var periodPostfix = "m";
        
        foreach (var arg in args)
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

        if (!command.StartsWith("/help"))
        {
            logger.LogInformation($"Command '{command}' will be executed with parameters: " +
                                  $"Period = {periodPrefix}{periodPostfix}, " +
                                  $"TopCount = {topCount}");
        }
        
        switch (command)
        {
            case "/help":
                logger.LogInformation("Executing /help command");
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
                var topInteractions = await GetTopInteractionsAsync(period, periodPrefix, periodPostfix, topCount, cancellationToken);
                await botClient.SendMessage(chatId, topInteractions, parseMode: ParseMode.Html, cancellationToken: cancellationToken);
                break;
            case "/topphotos":
                var topPhotos = await GetTopPhotosAsync(chatId, period, periodPrefix, periodPostfix, topCount, cancellationToken);
                await botClient.SendMessage(chatId, topPhotos, parseMode: ParseMode.Html, cancellationToken: cancellationToken);
                break;
            case "/topreactions":
                var topReactions = await GetTopReactionsAsync(period, periodPrefix, periodPostfix, topCount, cancellationToken);
                await botClient.SendMessage(chatId, topReactions, parseMode: ParseMode.Html, cancellationToken: cancellationToken);
                break;
            default:
                logger.LogWarning($"Unknown command received: {command}");
                break;
        }
    }

    public async Task<string> GetTopUsersAsync(long chatId, DateTime period, int periodPrefix, string periodPostfix,
        int topCount,
        CancellationToken cancellationToken)
    {
        var userStats = await dbContext.PhotoMessages
            .Where(p => p.CreatedAt >= period && p.ChatId == chatId)
            .Join(
                dbContext.UserStats,
                photo => photo.UserId,
                userStat => userStat.Id,
                (photo, userStat) => new { Photo = photo, UserStat = userStat }
            )
            .GroupJoin(
                dbContext.Reactions,
                x => x.Photo.MessageId,
                reaction => reaction.MessageId,
                (x, reactions) => new {
                    x.Photo.UserId,
                    x.UserStat.Username,
                    ReactionsCount = reactions.Count()
                }
            )
            .GroupBy(x => new { x.UserId, x.Username })
            .Select(g => new {
                Username = g.Key.Username,
                TotalReactions = g.Sum(x => x.ReactionsCount),
                PhotosCount = g.Count()
            })
            .OrderByDescending(x => x.TotalReactions)
            .Take(topCount)
            .ToListAsync(cancellationToken);
    
        var resultMsg = new StringBuilder($"<b>На их фото реагировали больше всего!</b>\n<i>(за {periodPrefix}{periodPostfix})</i>\n\n"); 
    
        if (userStats.Count == 0)
        {
            return $"Нет данных о реакциях на фото пользователей за {periodPrefix}{periodPostfix}.";
        }

        var index = 1;
        foreach (var user in userStats)
        {
            resultMsg.AppendLine(
                $"<b>{index++}. @{user.Username}</b> - " +
                $"<b>{user.TotalReactions} шт.</b> (за {user.PhotosCount} фото)");
        }

        return resultMsg.ToString();
    }

    private async Task<string> GetTopInteractionsAsync(DateTime period, int periodPrefix, string periodPostfix,
        int topCount,
        CancellationToken cancellationToken)
    {
        var topUsers = await dbContext.UserStats
            .AsNoTracking()
            .Where(u => u.Id != 0)
            .OrderByDescending(u => u.TotalReactions)
            .Take(topCount)
            .ToListAsync(cancellationToken: cancellationToken);

        var resultMsg = new StringBuilder($"<b>Кто же поставил больше всего реакций?</b>\n<i>(за {periodPrefix}{periodPostfix}, (на свои + на чужие) )</i>\n");
        var index = 1;
        foreach (var u in topUsers)
        {
            resultMsg.Append($"<b>{index++}. @{u.Username}</b> - {u.TotalReactions} реакций ({u.TotalReactionsOnOwnMessages} + {u.TotalReactionsOnOthersMessages})");
            resultMsg.Append('\n');
        }
        return topUsers.Count != 0
            ? resultMsg.ToString()
            : $"Пока нет данных о реакциях за {periodPrefix}{periodPostfix}.";
    }


    public async Task<string> GetTopPhotosAsync(long chatId, DateTime period, int periodPrefix, string periodPostfix, int topCount,
        CancellationToken cancellationToken)
    {
        var topPhotos = await dbContext.PhotoMessages
            .AsNoTracking()
            .Where(p => p.CreatedAt > period && p.ChatId == chatId)
            .Join(dbContext.Reactions, 
                photo => photo.MessageId, 
                reaction => reaction.MessageId, 
                (photo, reaction) => new { photo, reaction })
            .GroupBy(pr => pr.photo.MessageId)
            .Select(group => new
            {
                MessageId = group.Key,
                TotalReactions = group.Count(),
                ChatId = group.First().photo.ChatId
            })
            .OrderByDescending(p => p.TotalReactions)
            .Take(topCount)
            .ToListAsync(cancellationToken: cancellationToken);

        var resultMsg = new StringBuilder($"<b>Какие фото вызвали больше всего изумления?</b>\n<i>(за {periodPrefix}{periodPostfix})</i>\n");
        var index = 1;

        foreach (var p in topPhotos)
        {
            resultMsg.Append(
                $"<a href=\"https://t.me/c/{p.ChatId.ToString()[4..]}/{p.MessageId}\">{index++} Место</a> - {p.TotalReactions} шт.");
            resultMsg.Append('\n');
        }
    
        return topPhotos.Count != 0
            ? resultMsg.ToString()
            : $"Нет данных о фотографиях за {periodPrefix}{periodPostfix}.";
    }


    private async Task<string> GetTopReactionsAsync(DateTime period, int periodPrefix, string periodPostfix,
        int topCount,
        CancellationToken cancellationToken)
    {
        var photoMsgIds = await dbContext.PhotoMessages.AsNoTracking().Select(pm => pm.MessageId).ToListAsync(cancellationToken: cancellationToken);
        
        var topReactions = await dbContext.Reactions
            .AsNoTracking()
            .Where(r => r.CreatedAt > period && photoMsgIds.Contains((int)r.MessageId))
            .GroupBy(r => r.Emoji)
            .Select(group => new
            {
                Emoji = group.Key,
                Count = group.Count()
            })
            .OrderByDescending(r => r.Count)
            .Take(topCount)
            .ToListAsync(cancellationToken);

        var resultMsg = new StringBuilder($"<b>Как мы реагировали чаще всего?</b>\n<i>(за {periodPrefix}{periodPostfix}, без кастомных эмодзи)</i>\n");
        var index = 1;
    
        foreach (var r in topReactions.Where(r => r.Emoji!.Length <= 4))
        {
            resultMsg.Append( $"{index++}. {r.Emoji} - {r.Count} шт.");
            resultMsg.Append('\n');
        }
    
        return topReactions.Count != 0
            ? resultMsg.ToString()
            : $"Нет данных о реакциях за {periodPrefix}{periodPostfix}.";
    }

    public async Task SendMessage(long chatId, string topUserMsg, CancellationToken token)
    {
        await botClient.SendMessage(chatId, topUserMsg, parseMode: ParseMode.Html, cancellationToken: token);
    }
}