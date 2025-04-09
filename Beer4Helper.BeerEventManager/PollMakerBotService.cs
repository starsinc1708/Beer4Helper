using Beer4Helper.Shared;
using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Beer4Helper.BeerEventManager;
public class PollMakerBotService(
    ITelegramBotClient botClient,
    TgBotSettings botSettings,
    ILogger<PollMakerBotService> logger,
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
            
        }
    }

    public async Task CreateNewPoll(CancellationToken ct)
    {
        var chatIds = botSettings.BotModules!
            .FirstOrDefault(m => m.Key.Equals("BeerEventManager"))
            .Value.ParsedAllowedChats!.FirstOrDefault(chats => chats.Key.Equals(UpdateSource.Channel)).Value;
        logger.LogInformation($"Creating new poll for {string.Join(',', chatIds)}");
    }
}