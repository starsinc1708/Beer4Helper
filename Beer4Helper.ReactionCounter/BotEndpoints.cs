using Telegram.Bot.Requests;

namespace Beer4Helper.ReactionCounter;

public static class BotEndpoints
{
    public static void MapTelegramEndpoints(this WebApplication app)
    {
        app.MapGet("/check", () => Results.Ok("OK"));
        
        app.MapPost("/api/sendTopStats", async (HttpContext context, TelegramBotService botService) =>
        {
            try
            {
                var request = await context.Request.ReadFromJsonAsync<TopStatsRequest>();
                if (request == null || request.ChatId == 0 || request.SendToChatId == 0)
                {
                    return Results.BadRequest("Invalid request parameters");
                }

                var period = DateTime.UtcNow.AddMonths(-1);
                var periodPrefix = 1;
                var periodPostfix = "m";
                
                var topReactions = await botService.GetTopReactionsAsync(request.ChatId, period, periodPrefix, periodPostfix, 20, context.RequestAborted);
                await botService.SendMessage(request.SendToChatId, topReactions, context.RequestAborted);
                
                var topUserMsg = await botService.GetTopUsersAsync(request.ChatId, period, periodPrefix, periodPostfix, 15, context.RequestAborted);
                await botService.SendMessage(request.SendToChatId, topUserMsg, context.RequestAborted);
                
                var topPhotos = await botService.GetTopPhotosAsync(request.ChatId, period, periodPrefix, periodPostfix, 10, context.RequestAborted);
                await botService.SendMessage(request.SendToChatId, topPhotos, context.RequestAborted);

                return Results.Ok("Top stats sent successfully");
            }
            catch (Exception ex)
            {
                return Results.Problem($"Error sending top stats: {ex.Message}", statusCode: 500);
            }
        });

        app.MapPost("/api/sendCustomTopStats", async (HttpContext context, TelegramBotService botService) =>
        {
            try
            {
                var request = await context.Request.ReadFromJsonAsync<CustomTopStatsRequest>();
                if (request == null || request.ChatId == 0 || request.SendToChatId == 0)
                {
                    return Results.BadRequest("Invalid request parameters");
                }

                DateTime period;
                int periodPrefix;
                string periodPostfix;
                
                if (request.Period.EndsWith('d') && int.TryParse(request.Period[..^1], out var days))
                {
                    period = DateTime.UtcNow.AddDays(-days);
                    periodPrefix = days;
                    periodPostfix = "d";
                }
                else if (request.Period.EndsWith('w') && int.TryParse(request.Period[..^1], out var weeks))
                {
                    period = DateTime.UtcNow.AddDays(-weeks * 7);
                    periodPrefix = weeks;
                    periodPostfix = "w";
                }
                else if (request.Period.EndsWith('m') && int.TryParse(request.Period[..^1], out var months))
                {
                    period = DateTime.UtcNow.AddMonths(-months);
                    periodPrefix = months;
                    periodPostfix = "m";
                }
                else
                {
                    period = DateTime.UtcNow.AddMonths(-1);
                    periodPrefix = 1;
                    periodPostfix = "m";
                }

                var topCount = Math.Clamp(request.TopCount, 1, 25);

                switch (request.TopType)
                {
                    case "u":
                        var topUserMsg = await botService.GetTopUsersAsync(
                            request.ChatId, 
                            period, 
                            periodPrefix, 
                            periodPostfix, 
                            topCount, 
                            context.RequestAborted);
                        await botService.SendMessage(request.SendToChatId, topUserMsg, context.RequestAborted);
                        break;
                    case "r":
                        var topReactions = await botService.GetTopReactionsAsync(
                            request.ChatId, 
                            period, 
                            periodPrefix, 
                            periodPostfix, 
                            topCount, 
                            context.RequestAborted);
                        await botService.SendMessage(request.SendToChatId, topReactions, context.RequestAborted);
                        break;
                    case "p":
                        var topPhotos = await botService.GetTopPhotosAsync(
                            request.ChatId, 
                            period,
                            periodPrefix,
                            periodPostfix,
                            topCount, 
                            context.RequestAborted);
                        await botService.SendMessage(request.SendToChatId, topPhotos, context.RequestAborted);
                        break;
                    case "i":
                        var topInteractions = await botService.GetTopInteractionsAsync(
                            request.ChatId, period,
                            periodPrefix,
                            periodPostfix,
                            topCount, 
                            context.RequestAborted);
                        await botService.SendMessage(request.SendToChatId, topInteractions, context.RequestAborted);
                        break;
                    case "all":
                        var reactions = await botService.GetTopReactionsAsync(
                            request.ChatId, 
                            period, 
                            periodPrefix, 
                            periodPostfix, 
                            topCount, 
                            context.RequestAborted);
                        var users = await botService.GetTopUsersAsync(
                            request.ChatId, 
                            period, 
                            periodPrefix, 
                            periodPostfix, 
                            topCount, 
                            context.RequestAborted);
                        var photos = await botService.GetTopPhotosAsync(
                            request.ChatId, 
                            period,
                            periodPrefix,
                            periodPostfix,
                            topCount, 
                            context.RequestAborted);
                        var allInOne = photos + "\n\n" + users + "\n\n" + reactions;
                        await botService.SendMessage(request.SendToChatId, allInOne, context.RequestAborted);
                        break;
                    default:
                        return Results.BadRequest("Invalid request parameters");
                }
                return Results.Ok("Custom top stats sent successfully");
            }
            catch (Exception ex)
            {
                return Results.Problem($"Error sending custom top stats: {ex.Message}", statusCode: 500);
            }
        });

        app.MapPost("/api/getChatMembers", async (HttpContext context, TelegramBotService botService) =>
        {
            var request = await context.Request.ReadFromJsonAsync<ChatMemberRequest>();
            if (request == null || request.ChatId == 0 || request.ChatId == 0)
            {
                return Results.BadRequest("Invalid request parameters");
            }
            var chatMembers = await botService.GetChatMembers(request.ChatId, context.RequestAborted);
            return Results.Ok(chatMembers);
        });
        
        app.MapPost("/api/generateUserStats", async (HttpContext context, TelegramBotService botService) =>
        {
            var request = await context.Request.ReadFromJsonAsync<ChatMemberRequest>();
            if (request == null || request.ChatId == 0 || request.ChatId == 0)
            {
                return Results.BadRequest("Invalid request parameters");
            }
            var chatMembers = await botService.UpdateUserStatForChat(request.ChatId, context.RequestAborted);
            return Results.Ok(chatMembers);
        });

        app.MapPost("/api/editMessage", async (HttpContext context, TelegramBotService botService) =>
        {
            var request = await context.Request.ReadFromJsonAsync<EditMessageRequest>();
            if (request == null || request.ChatId == 0 || request.MessageId == 0)
            {
                return Results.BadRequest("Invalid request parameters");
            }
            await botService.EditMessage(request.ChatId, request.MessageId, request.Type, DateTime.Now, context.RequestAborted);
            return Results.Ok("message edited");
        });
    }
}

public class ChatMemberRequest {
    public long ChatId { get; set; }
}

public class EditMessageRequest
{
    public long ChatId { get; set; }
    public long MessageId { get; set; }
    public required string Type { get; set; }
}

public class TopStatsRequest
{
    public long ChatId { get; set; }
    public long SendToChatId { get; set; }
}

public class CustomTopStatsRequest
{
    public long ChatId { get; set; }
    public long SendToChatId { get; set; }
    public string Period { get; set; } = "1m";
    public int TopCount { get; set; } = 10;
    
    public required string TopType { get; set; }
}