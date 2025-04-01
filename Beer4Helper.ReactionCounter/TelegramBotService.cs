using System.Globalization;
using System.Text;
using Beer4Helper.ReactionCounter.Data;
using Beer4Helper.ReactionCounter.Models;
using Beer4Helper.ReactionCounter.StatModels;
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
            logger.LogInformation(update.Type == UpdateType.MyChatMember
                ? $"My chat member update: {update.MyChatMember!.Chat.Id}"
                : $"Update received: {update.Type}");
            
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
            if (oldReactions.Any(r => ExtractReactionValue(r) == emoji)) continue;
            await SaveReaction(chatId, userId, username, messageId, emoji, cancellationToken);
            logger.LogInformation($"message reaction saved [{emoji} \t from {username}]");
        }
        
        foreach (var reaction in oldReactions)
        {
            var emoji = ExtractReactionValue(reaction);
            if (newReactions.Any(r => ExtractReactionValue(r) == emoji)) continue;
            await RemoveReaction(chatId, userId, messageId, emoji, cancellationToken);
            logger.LogInformation($"message reaction removed [{emoji} \t from {username}]");
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
    
    

    private async Task HandleMessage(Message message, CancellationToken cancellationToken)
    {
        if (message.Chat.Type == ChatType.Private) return;
        if (message.Photo != null)
        {
            if (_settings.ReactionChatIds.Contains(message.Chat.Id))
            {
                await SavePhotoMessage(message, cancellationToken);
                logger.LogInformation($"photo message saved [from {message.From!.Username}]");
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

    public async Task<List<ChatMember>> GetChatMembers(long chatId, CancellationToken cancellationToken)
    {
        var userIdsFromDb = await dbContext.Reactions
            .AsNoTracking()
            .Where(r => r.ChatId == chatId)
            .Select(r => r.UserId).Distinct()
            .ToListAsync(cancellationToken);
        
        var chatMembers = new List<ChatMember>();
        foreach (var userId in userIdsFromDb)
        {
            chatMembers.Add(await botClient.GetChatMember(chatId, userId, cancellationToken));
        }
        return chatMembers;
    }

    public async Task<List<UserStats>> GenerateUserStats(long chatId, CancellationToken cancellationToken)
    {
        var chatMembers = await GetChatMembers(chatId, cancellationToken);
        
        var allPhotoMessages = await dbContext.PhotoMessages
            .Where(pm => pm.ChatId == chatId)
            .OrderBy(pm => pm.UserId)
            .ThenBy(pm => pm.CreatedAt)
            .ToListAsync(cancellationToken);
        
        var userPhotoGroups = allPhotoMessages
            .GroupBy(pm => pm.UserId)
            .ToList();
        
        var uniqueMessagesCount = new Dictionary<long, int>();

        foreach (var userGroup in userPhotoGroups)
        {
            var count = 0;
            PhotoMessage? previousMessage = null;
            
            foreach (var message in userGroup)
            {
                if (previousMessage == null || 
                    (message.CreatedAt - previousMessage.CreatedAt).TotalSeconds > 2)
                {
                    count++;
                }
                previousMessage = message;
            }
            
            uniqueMessagesCount[userGroup.Key] = count;
        }

        var reactionsStats = await dbContext.Reactions
            .Where(r => r.ChatId == chatId)
            .GroupBy(r => r.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                TotalReactions = g.Count(),
                ReactionsOnOwnMessages = dbContext.Reactions
                    .Join(dbContext.PhotoMessages,
                        r => r.MessageId,
                        pm => pm.MessageId,
                        (r, pm) => new { Reaction = r, PhotoMessage = pm })
                    .Count(x => x.Reaction.UserId == g.Key && 
                              x.PhotoMessage.UserId == g.Key && 
                              x.Reaction.ChatId == chatId)
            })
            .ToListAsync(cancellationToken);
        
        var photosStats = await dbContext.PhotoMessages
            .Where(pm => pm.ChatId == chatId)
            .GroupBy(pm => pm.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                TotalPhotosUploaded = g.Count()
            })
            .ToListAsync(cancellationToken);

        return (from member in chatMembers
            let userId = member.User.Id
            let username = member.User.Username ?? string.Empty
            let reactionsStat = reactionsStats.FirstOrDefault(s => s.UserId == userId)
            let photosStat = photosStats.FirstOrDefault(s => s.UserId == userId)
            select new UserStats
            {
                Id = $"{userId}:{chatId}",
                Username = username,
                ChatId = chatId,
                UserId = userId,
                TotalReactions = reactionsStat?.TotalReactions ?? 0,
                TotalReactionsOnOwnMessages = reactionsStat?.ReactionsOnOwnMessages ?? 0,
                TotalReactionsOnOthersMessages = (reactionsStat?.TotalReactions ?? 0) - 
                                               (reactionsStat?.ReactionsOnOwnMessages ?? 0),
                TotalPhotosUploaded = photosStat?.TotalPhotosUploaded ?? 0,
                TotalUniqueMessages = uniqueMessagesCount.GetValueOrDefault(userId, 0)
            }).ToList();
    }
    
    public async Task UpdateUserStats(CancellationToken stoppingToken)
    {
        foreach (var chatId in await dbContext.Reactions.Select(r => r.ChatId).Distinct().ToListAsync(stoppingToken))
        {
            await UpdateUserStatForChat(chatId, stoppingToken);
        }
    }

    public async Task<List<UserStats>> UpdateUserStatForChat(long chatId, CancellationToken stoppingToken)
    {
        try
        {
            var stats = await GenerateUserStats(chatId, stoppingToken);
            
            foreach (var stat in stats)
            {
                var existingStat = await dbContext.UserStats
                    .FirstOrDefaultAsync(s => s.Id == stat.Id && s.ChatId == chatId, stoppingToken);

                if (existingStat == null)
                {
                    dbContext.UserStats.Add(stat);
                }
                else
                {
                    existingStat.Username = stat.Username;
                    existingStat.TotalReactions = stat.TotalReactions;
                    existingStat.TotalReactionsOnOwnMessages = stat.TotalReactionsOnOwnMessages;
                    existingStat.TotalReactionsOnOthersMessages = stat.TotalReactionsOnOthersMessages;
                    existingStat.TotalPhotosUploaded = stat.TotalPhotosUploaded;
                    existingStat.TotalUniqueMessages = stat.TotalUniqueMessages;
                }
            }
                
            await dbContext.SaveChangesAsync(stoppingToken);
            logger.LogInformation("Updated stats for chat {ChatId}", chatId);
            return stats;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error updating stats for chat {chatId}");
            return [];
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
        }
    }

    public async Task<string> GetTopUsersAsync(long chatId, DateTime period, int periodPrefix, string periodPostfix,
        int topCount, CancellationToken cancellationToken)
    {
        var userStats = await dbContext.UserStats
            .Where(u => u.ChatId == chatId)
            .Select(u => new
            {
                u.Username,
                u.TotalReactions,
                u.TotalPhotosUploaded,
                RecentReactions = dbContext.Reactions
                    .Count(r => r.ChatId == chatId && 
                                dbContext.PhotoMessages.Any(p => 
                                    p.MessageId == r.MessageId && 
                                    p.UserId == u.UserId &&
                                    p.CreatedAt >= period)),
                RecentPhotos = dbContext.PhotoMessages
                    .Count(p => p.ChatId == chatId && 
                                p.UserId == u.UserId &&
                                p.CreatedAt >= period)
            })
            .OrderByDescending(u => u.RecentReactions)
            .Take(topCount)
            .ToListAsync(cancellationToken);

        var resultMsg = new StringBuilder($"<b>На их фото реагировали больше всего!</b>\n");

        if (userStats.Count == 0)
        {
            return $"Нет данных о реакциях на фото пользователей за {periodPrefix}{periodPostfix}.";
        }

        var index = 1;
        foreach (var user in userStats)
        {
            resultMsg.AppendLine(
                $"<b>{index++}. @{user.Username}</b> - <b>{user.RecentReactions} шт.</b> (<b>{user.RecentPhotos}</b> фото)");
        }

        return resultMsg.ToString();
    }

    public async Task<string> GetTopInteractionsAsync(long chatId, DateTime period, int periodPrefix, string periodPostfix,
        int topCount,
        CancellationToken cancellationToken)
    {
        var topUsers = await dbContext.UserStats
            .AsNoTracking()
            .Where(u => u.UserId != 0 && u.ChatId == chatId)
            .OrderByDescending(u => u.TotalReactions)
            .Take(topCount)
            .ToListAsync(cancellationToken: cancellationToken);

        var resultMsg = new StringBuilder($"<b>Кто же поставил больше всего реакций?</b>\n<i>(за {periodPrefix}{periodPostfix}, (на свои + на чужие) )</i>\n\n");
        var index = 1;
        foreach (var u in topUsers)
        {
            resultMsg.Append($"<b>{index++}. @{u.Username}</b> - <b>{u.TotalReactions}</b> шт. ({u.TotalReactionsOnOwnMessages} + {u.TotalReactionsOnOthersMessages})");
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

        var resultMsg = new StringBuilder($"<b>Какие фото вызвали больше всего изумления?</b>\n");
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


    public async Task<string> GetTopReactionsAsync(long chatId, DateTime period, int periodPrefix, string periodPostfix,
        int topCount,
        CancellationToken cancellationToken)
    {
        var photoMsgIds = await dbContext.PhotoMessages.AsNoTracking().Select(pm => pm.MessageId).ToListAsync(cancellationToken: cancellationToken);
        
        var topReactions = await dbContext.Reactions
            .AsNoTracking()
            .Where(r => r.CreatedAt > period && r.ChatId == chatId && photoMsgIds.Contains((int)r.MessageId))
            .GroupBy(r => r.Emoji)
            .Select(group => new
            {
                Emoji = group.Key,
                Count = group.Count()
            })
            .OrderByDescending(r => r.Count)
            .Take(topCount)
            .ToListAsync(cancellationToken);

        var resultMsg = new StringBuilder($"<b>Как мы реагировали чаще всего?</b>\n");
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

    public async Task EditMessage(long chatId, long messageId, string text, CancellationToken token)
    {
        await botClient.EditMessageText(new ChatId(chatId), (int)messageId, text, ParseMode.Html, cancellationToken: token);
    }
    
    public async Task DeleteMessage(long chatId, long messageId, DateTime now, CancellationToken token)
    {
        await botClient.DeleteMessage(new ChatId(chatId), (int)messageId, cancellationToken: token);
    }
    
    public async Task CreateTopMessageTest(long chatId, long sendTo, CancellationToken token)
    {
        var now = DateTime.UtcNow;
        var firstDayOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        const int topCount = 10;

        var photoStats = await GetPhotoStatsAsync(chatId, firstDayOfMonth, topCount, token);
        var userStats = await GetUserStatsAsync(chatId, firstDayOfMonth, topCount, token);
        var reactionStats = await GetReactionStatsAsync(chatId, firstDayOfMonth, topCount, token);
    
        var russianCulture = new CultureInfo("ru-RU");
        var monthName = russianCulture.DateTimeFormat.GetMonthName(now.Month);
        
        var header = $"<b>СТАТИСТИКА ЧАТА</b> \u2757\ufe0f\n<i>за {monthName} {now.Year}</i>";
        var footerText =  $"<i>Последнее обновление {now + TimeSpan.FromHours(4):HH:mm dd/MM/yyyy}</i>";
        
        var messageText = ConstructTopMessage(header, photoStats, userStats, reactionStats, footerText);
    
        await botClient.SendMessage(sendTo, messageText, parseMode: ParseMode.Html, cancellationToken: token);
    }
    
    public async Task CreateTopMessageAndSend(long chatId, CancellationToken token)
    {
        var existingTopMsgs = await dbContext.TopMessages.Where(m => m.ChatId == chatId).ToListAsync(token);
        if (existingTopMsgs.Count != 0)
        {
            foreach (var msg in existingTopMsgs)
            {
                await botClient.UnpinChatMessage(chatId, msg.MessageId, cancellationToken: token);
            }
            dbContext.TopMessages.RemoveRange(existingTopMsgs);
            await dbContext.SaveChangesAsync(token);
        }
        
        var now = DateTime.UtcNow;
        var firstDayOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        const int topCount = 10;

        var photoStats = await GetPhotoStatsAsync(chatId, firstDayOfMonth, topCount, token);
        var userStats = await GetUserStatsAsync(chatId, firstDayOfMonth, topCount, token);
        var reactionStats = await GetReactionStatsAsync(chatId, firstDayOfMonth, topCount, token);
    
        var russianCulture = new CultureInfo("ru-RU");
        var monthName = russianCulture.DateTimeFormat.GetMonthName(now.Month);
        
        var header = $"<b>СТАТИСТИКА ЧАТА</b> \u2757\ufe0f\n<i>за {monthName} {now.Year}</i>";
        var footerText =  $"<i>Последнее обновление {now + TimeSpan.FromHours(4):HH:mm dd/MM/yyyy}</i>";
        var messageText = ConstructTopMessage(header, photoStats, userStats, reactionStats, footerText);
    
        var topMessage = new TopMessage
        {
            ChatId = chatId,
            Text = messageText,
            MessageId = -1
        };
    
        var sentMsg = await botClient.SendMessage(chatId, topMessage.Text, parseMode: ParseMode.Html, cancellationToken: token);
        await botClient.PinChatMessage(chatId, sentMsg.MessageId, disableNotification: true, cancellationToken: token);
        
        topMessage.MessageId = sentMsg.MessageId;
        topMessage.EditedAt = sentMsg.Date;
    
        await dbContext.AddAsync(topMessage, token);
        await dbContext.SaveChangesAsync(token);
    }
    
    public async Task UpdateAllTopMessages(CancellationToken token)
    {
        var now = DateTime.UtcNow;
        var firstDayOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var allTopMessages = await dbContext.TopMessages.ToListAsync(token);
    
        foreach (var topMessage in allTopMessages)
        {
            try
            {
                var photoStats = await GetPhotoStatsAsync(topMessage.ChatId, firstDayOfMonth, 10, token);
                var userStats = await GetUserStatsAsync(topMessage.ChatId, firstDayOfMonth, 15, token);
                var reactionStats = await GetReactionStatsAsync(topMessage.ChatId, firstDayOfMonth, 25, token);
            
                var russianCulture = new CultureInfo("ru-RU");
                var monthName = russianCulture.DateTimeFormat.GetMonthName(now.Month);
                
                var header = $"<b>СТАТИСТИКА ЧАТА</b> \u2757\ufe0f\n<i>за {monthName} {now.Year}</i>";
                var footerText =  $"<i>Последнее обновление {now + TimeSpan.FromHours(4):HH:mm dd/MM/yyyy}</i>";
                
                var messageText = ConstructTopMessage(header, photoStats, userStats, reactionStats, footerText);

                if (messageText == topMessage.Text) continue;
                topMessage.Text = messageText;
                topMessage.EditedAt = now;
                
                await dbContext.SaveChangesAsync(token);
                await EditMessage(topMessage.ChatId, topMessage.MessageId, messageText, token);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error updating top message for chat {topMessage.ChatId}");
            }
        }
    }

    private async Task<List<PhotoStat>> GetPhotoStatsAsync(long chatId, DateTime period, int topCount,
        CancellationToken cancellationToken)
    {
        return await dbContext.PhotoMessages
            .AsNoTracking()
            .Where(p => p.CreatedAt > period && p.ChatId == chatId)
            .GroupJoin(dbContext.Reactions,
                photo => photo.MessageId,
                reaction => reaction.MessageId,
                (photo, reactions) => new { Photo = photo, Reactions = reactions })
            .SelectMany(x => x.Reactions
                    .Where(r => r.UserId != x.Photo.UserId) // Только реакции других пользователей
                    .DefaultIfEmpty(),
                (x, reaction) => new { x.Photo, Reaction = reaction })
            .Where(x => x.Reaction != null) // Исключаем записи без реакций
            .GroupBy(x => x.Photo.MessageId)
            .Select(group => new PhotoStat
            {
                MessageId = group.Key,
                TotalReactions = group.Count(),
                ChatId = group.First().Photo.ChatId
            })
            .OrderByDescending(p => p.TotalReactions)
            .Take(topCount)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<UserStat>> GetUserStatsAsync(long chatId, DateTime period, int topCount,
        CancellationToken cancellationToken)
    {
        return await dbContext.UserStats
            .Where(u => u.ChatId == chatId)
            .Select(u => new UserStat
            {
                Username = u.Username ?? "@anonimus",
                TotalReactions = dbContext.Reactions
                    .Count(r => r.ChatId == chatId && r.UserId != u.UserId && 
                                dbContext.PhotoMessages.Any(p => 
                                    p.MessageId == r.MessageId && 
                                    p.UserId == u.UserId &&
                                    p.CreatedAt >= period)),
                TotalPhotos = dbContext.PhotoMessages
                    .Count(p => p.ChatId == chatId && 
                                p.UserId == u.UserId &&
                                p.CreatedAt >= period)
            })
            .OrderByDescending(u => u.TotalReactions)
            .Take(topCount)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<ReactionStat>> GetReactionStatsAsync(long chatId, DateTime period, int topCount,
        CancellationToken cancellationToken)
    {
        var photoMsgIds = await dbContext.PhotoMessages
            .AsNoTracking()
            .Where(p => p.ChatId == chatId && p.CreatedAt >= period)
            .Select(p => p.MessageId)
            .ToListAsync(cancellationToken);

        return await dbContext.Reactions
            .AsNoTracking()
            .Where(r => r.CreatedAt > period && 
                        r.ChatId == chatId && 
                        photoMsgIds.Contains((int)r.MessageId))
            .GroupBy(r => r.Emoji)
            .Select(group => new ReactionStat
            {
                Emoji = group.Key ?? "custom_emoji",
                Count = group.Count()
            })
            .OrderByDescending(r => r.Count)
            .Take(topCount)
            .ToListAsync(cancellationToken);
    }
    
    private static string ConstructTopMessage(
        string header, 
        List<PhotoStat> photoStats,
        List<UserStat> userStats,
        List<ReactionStat> reactionStats, 
        string footerText)
    {
        var messageBuilder = new StringBuilder(header);
        
        messageBuilder.AppendLine("<blockquote expandable><b>Эти фото</b> <s>почти</s> <b>никого не оставили равнодушным:</b>");
        if (photoStats.Count > 0)
        {
            for (var i = 0; i < photoStats.Count; i++)
            {
                var photo = photoStats[i];
                messageBuilder.AppendLine(
                    $"{i + 1}. <a href=\"https://t.me/c/{photo.ChatId.ToString()[4..]}/{photo.MessageId}\">Фото</a> - {photo.TotalReactions} шт.");
            }
        }
        else
        {
            messageBuilder.AppendLine("Нет данных о фотографиях");
        }
        messageBuilder.Append("</blockquote>");
        
        messageBuilder.AppendLine("<blockquote expandable><b>На их фото реагировали больше всего:</b>");
        userStats = userStats.FindAll(us => us.TotalPhotos > 0);
        if (userStats.Count > 0)
        {
            for (var i = 0; i < userStats.Count; i++)
            {
                var user = userStats[i];
                
                messageBuilder.AppendLine(
                    $"{i + 1}. @{user.Username} - {user.TotalReactions} шт. ({user.TotalPhotos} фото)");
            }
        }
        else
        {
            messageBuilder.AppendLine("Нет данных о пользователях");
        }
        messageBuilder.Append("</blockquote>");
        
        messageBuilder.AppendLine("<blockquote expandable><b>Самые популярные реакции:</b>");
        if (reactionStats.Count > 0)
        {
            for (var i = 0; i < reactionStats.Count; i++)
            {
                var reaction = reactionStats[i];
                messageBuilder.AppendLine($"{i + 1}. {reaction.Emoji} - {reaction.Count} шт.");
            }
        }
        else
        {
            messageBuilder.AppendLine("Нет данных о реакциях");
        }
        messageBuilder.Append("</blockquote>");
        
        messageBuilder.AppendLine(footerText);        
        
        return messageBuilder.ToString();
    }
}