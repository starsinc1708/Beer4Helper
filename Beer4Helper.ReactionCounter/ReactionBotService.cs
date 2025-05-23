﻿using System.Globalization;
using System.Text;
using Beer4Helper.ReactionCounter.Handlers;
using Beer4Helper.ReactionCounter.Models;
using Beer4Helper.ReactionCounter.Models.StatModels;
using Beer4Helper.Shared;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Beer4Helper.ReactionCounter;

public class ReactionBotService(
    ITelegramBotClient botClient,
    ReactionHandler reactionHandler,
    MessageHandler messageHandler,
    ILogger<ReactionBotService> logger,
    ReactionDbContext dbContext)
{
    public async Task HandleUpdate(TgUpdateRequest? request, CancellationToken ct)
    {
        if  (request == null) return;

        var update = request.Update!;
        
        if (request.Type!.Equals(UpdateType.Message.ToString()))
        {
            await messageHandler.ProcessUpdate(update, ct);
        }
        else if (request.Type!.Equals(UpdateType.MessageReaction.ToString()))
        {
            await reactionHandler.ProcessUpdate(update, ct);
        }
    }
    
    private async Task<List<ChatMember>> GetChatMembers(long chatId, CancellationToken cancellationToken)
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
    
    public async Task UpdateUserStats(CancellationToken stoppingToken)
    {
        foreach (var chatId in await dbContext.Reactions.Select(r => r.ChatId).Distinct().ToListAsync(stoppingToken))
        {
            await UpdateUserStatForChat(chatId, stoppingToken);
        }
    }

    private async Task UpdateUserStatForChat(long chatId, CancellationToken stoppingToken)
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
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error updating stats for chat {chatId}");
        }
    }
    
    private async Task<List<UserStats>> GenerateUserStats(long chatId, CancellationToken cancellationToken)
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
                    Math.Abs((message.CreatedAt - previousMessage.CreatedAt).TotalSeconds) > 2)
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
    
    public async Task EditMessage(long chatId, long messageId, string text, CancellationToken token)
    {
        await botClient.EditMessageText(new ChatId(chatId), (int)messageId, text, ParseMode.Html, cancellationToken: token);
    }
    
    public async Task DeleteMessage(long chatId, long messageId, CancellationToken token)
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
        
        var header = $"<b>СТАТИСТИКА ЧАТА</b> \u2757\ufe0f\n<i>за {monthName} {now.Year}</i>\n";
        var footerText =  $"<i>Последнее обновление: {now + TimeSpan.FromHours(4):HH:mm dd/MM/yyyy}</i>";
        
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
        
        var header = $"<b>СТАТИСТИКА ЧАТА</b> \u2757\ufe0f\n<i>за {monthName} {now.Year}</i>\n";
        var footerText =  $"<i>Последнее обновление: {now + TimeSpan.FromHours(4):HH:mm dd/MM/yyyy}</i>";
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

        var allTopMessages = await dbContext.TopMessages
            .GroupBy(m => m.ChatId)
            .Select(g => g.OrderByDescending(m => m.EditedAt).FirstOrDefault()!)
            .ToListAsync(token);
    
        foreach (var topMessage in allTopMessages)
        {
            try
            {
                var photoStats = await GetPhotoStatsAsync(topMessage.ChatId, firstDayOfMonth, 10, token);
                var userStats = await GetUserStatsAsync(topMessage.ChatId, firstDayOfMonth, 15, token);
                var reactionStats = await GetReactionStatsAsync(topMessage.ChatId, firstDayOfMonth, 25, token);
            
                var russianCulture = new CultureInfo("ru-RU");
                var monthName = russianCulture.DateTimeFormat.GetMonthName(now.Month);
                
                var header = $"<b>СТАТИСТИКА ЧАТА</b> \u2757\ufe0f\n<i>за {monthName} {now.Year}</i>\n";
                var footerText =  $"<i>Последнее обновление: {now + TimeSpan.FromHours(4):HH:mm dd/MM/yyyy}</i>";
                
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
                        r.Emoji!.Length < 4 &&
                        photoMsgIds.Contains((int)r.MessageId))
            .GroupBy(r => r.Emoji)
            .Select(group => new ReactionStat
            {
                Emoji = group.Key,
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