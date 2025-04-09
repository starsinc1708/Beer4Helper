using Beer4Helper.Shared;
using Telegram.Bot;

namespace Beer4Helper.BeerEventManager.Endpoints;

public static class BotEndpoints
{
    public static void MapTelegramEndpoints(this WebApplication app)
    {
        app.MapPost("/processUpdate", async (
            HttpContext context, 
            PollMakerBotService botService,
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

