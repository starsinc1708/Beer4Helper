﻿using Beer4Helper.ReactionCounter.Data;
using Beer4Helper.ReactionCounter.Models;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Beer4Helper.ReactionCounter;

public class TelegramBotService(
    ITelegramBotClient botClient,
    ILogger<TelegramBotService> logger,
    ReactionDbContext dbContext)
{
    public const long ALLOWED_CHAT_ID = -1002265344874;
    public const long CHAT_FOR_COMMANDS_ID = -1002265344874;
    
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
                    if (update.MessageReaction.Chat.Id == ALLOWED_CHAT_ID)
                        await HandleReactionUpdate(update.MessageReaction, cancellationToken);
                    break;
                case { Type: UpdateType.Message, Message: not null }:
                    logger.LogInformation($"Update received from chat[{update.Message!.Chat.Id}]");
                    if (update.Message.Chat.Id is (ALLOWED_CHAT_ID or CHAT_FOR_COMMANDS_ID))
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


    private string ExtractReactionValue(ReactionType reaction)
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
        if (message.Photo != null)
        {
            await SavePhotoMessage(message, cancellationToken);
        }
        else if (message.Text != null)
        {
            await HandleCommand(message, cancellationToken);
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
        var textParts = message.Text?.Trim().ToLower().Split("@")!;
        if (textParts.Length <= 1 || textParts[1] != "beer4helperbot") return;

        var command = textParts[0];

        switch (command)
        {
            case "/help":
                await botClient.SendMessage(chatId, 
                    "Команды:\n" +
                    "/topusers - топ пользователей.\n" +
                    "/topphotos - топ фото.\n" +
                    "/topreactions - топ реакций.",
                    parseMode: ParseMode.Html, cancellationToken: cancellationToken);
                break;
            case "/topusers":
                var topUsers = await GetTopUsersAsync(cancellationToken);
                await botClient.SendMessage(chatId, topUsers, parseMode: ParseMode.Html, cancellationToken: cancellationToken);
                break;
            case "/topphotos":
                var topPhotos = await GetTopPhotosAsync(cancellationToken);
                await botClient.SendMessage(chatId, topPhotos, parseMode: ParseMode.Html, cancellationToken: cancellationToken);
                break;
            case "/topreactions":
                var topReactions = await GetTopReactionsAsync(cancellationToken);
                await botClient.SendMessage(chatId, topReactions, parseMode: ParseMode.Html, cancellationToken: cancellationToken);
                break;
        }
    }
    
    private async Task<string> GetTopUsersAsync(CancellationToken cancellationToken)
    {
        var topUsers = await dbContext.UserStats
            .Where(u => u.Id != 0)
            .OrderByDescending(u => u.TotalReactions)
            .Take(10)
            .Select(u => $"<b>@{u.Username}</b> - {u.TotalReactions} реакций")
            .ToListAsync(cancellationToken: cancellationToken);

        return topUsers.Count != 0
            ? FormatTopList("Топ пользователей по количеству реакций\n", topUsers)
            : "Пока нет данных о реакциях.";
    }

    private async Task<string> GetTopPhotosAsync(CancellationToken cancellationToken)
    {
        var period = DateTime.UtcNow.AddMonths(-1);
    
        var topPhotos = await dbContext.PhotoMessages
            .Where(p => p.CreatedAt > period)
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
            .Take(10)
            .Select(p => $"<a href=\"https://t.me/c/{p.ChatId.ToString().Substring(4)}/{p.MessageId}\">Фото</a> - {p.TotalReactions} шт.")
            .ToListAsync(cancellationToken: cancellationToken);

        return topPhotos.Count != 0
            ? FormatTopList("Топ фотографий по количеству реакций", topPhotos)
            : "Пока нет данных о фотографиях.";
    }


    private async Task<string> GetTopReactionsAsync(CancellationToken cancellationToken)
    {
        var period = DateTime.UtcNow.AddMonths(-1);
        
        var topReactions = await dbContext.Reactions
            .Where(r => r.CreatedAt > period)
            .GroupBy(r => r.Emoji)
            .Select(group => new
            {
                Emoji = group.Key,
                Count = group.Count()
            })
            .OrderByDescending(r => r.Count)
            .Take(10)
            .Select(r => $"<b>{r.Emoji}</b> - {r.Count} шт.")
            .ToListAsync(cancellationToken);

        return topReactions.Count != 0
            ? FormatTopList("Топ самых частых реакций", topReactions)
            : "Пока нет данных о реакциях.";
    }

    private string FormatTopList(string title, List<string> list)
    {
        var formattedList = string.Join("\n", list);
        return $"<b>{title}</b>\n{formattedList}";
    }

    private string FormatTopList(string title, IEnumerable<object> list)
    {
        var formattedList = string.Join("\n", list.Select(item => item.ToString()));
        return $"<b>{title}</b>\n{formattedList}";
    }

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
}