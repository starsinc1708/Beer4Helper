using Beer4Helper.ReactionCounter.DTOs;
using Beer4Helper.Shared;
using Telegram.Bot;

namespace Beer4Helper.ReactionCounter.Endpoints;

public static class BotEndpoints
{
    public static void MapTelegramEndpoints(this WebApplication app)
    {
        app.MapPost("/bot/editMessage", async (HttpContext context, ReactionBotService botService) =>
        {
            var request = await context.Request.ReadFromJsonAsync<Requests.EditMessageRequest>();
            if (request == null || request.ChatId == 0 || request.MessageId == 0)
            {
                return Results.BadRequest("Invalid request parameters");
            }
            await botService.EditMessage(request.ChatId, request.MessageId, request.Text, context.RequestAborted);
            return Results.Ok("message edited");
        });
        
        app.MapPost("/bot/deleteMessage", async (HttpContext context, ReactionBotService botService) =>
        {
            var request = await context.Request.ReadFromJsonAsync<Requests.DeleteChatMessageRequest>();
            if (request == null || request.ChatId == 0 || request.MessageId == 0)
            {
                return Results.BadRequest("Invalid request parameters");
            }
            await botService.DeleteMessage(request.ChatId, request.MessageId, context.RequestAborted);
            return Results.Ok("message edited");
        });

        app.MapPost("/bot/reactions/createTopMessage", async (HttpContext context, ReactionBotService botService) =>
        {
            var request = await context.Request.ReadFromJsonAsync<Requests.CreateTopMessageRequest>();
            if (request == null || request.ChatId == 0)
            {
                return Results.BadRequest("Invalid request parameters");
            }
            await botService.CreateTopMessageAndSend(request.ChatId, context.RequestAborted);
            return Results.Ok("top message created");
        });
        
        app.MapPost("/bot/reactions/createTopMessageTest", async (HttpContext context, ReactionBotService botService) =>
        {
            var request = await context.Request.ReadFromJsonAsync<Requests.TopStatsRequest>();
            if (request == null || request.ChatId == 0)
            {
                return Results.BadRequest("Invalid request parameters");
            }
            await botService.CreateTopMessageTest(request.ChatId, request.SendToChatId, context.RequestAborted);
            return Results.Ok("top message created");
        });

        app.MapPost("/getUpdates", async (
            HttpContext context, 
            ReactionBotService botService,
            ILogger<Program> logger) =>
        {
            try
            {
                var data = await context.Request.ReadFromJsonAsync<TgUpdateRequest>(JsonBotAPI.Options, context.RequestAborted);
                if (data?.Update != null)
                {
                    await botService.HandleUpdate(data, context.RequestAborted);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error deserializing request");
            }
        });
    }
}

