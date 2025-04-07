using Beer4Helper.PollingService.Config;
using Beer4Helper.PollingService.Services;
using Beer4Helper.Shared;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Beer4Helper.PollingService.Polling;

public class UpdateDistributor(
    ModuleService moduleService,
    ILogger<UpdateDistributor> logger)
{
    public async Task DistributeUpdate(Update update, CancellationToken ct)
    {
        var type = update.Type;
        var source = ExtractUpdateSource(update);
        var fromId = ExtractFromId(update);
        
        logger.LogInformation($"Received Update [{source}-{type} FROM {fromId}]");

        await moduleService.SendUpdateToSuitableModules(update, type, source, fromId, ct);
    }

    private static long ExtractFromId(Update update)
    {
        return update.Type switch
        {
            UpdateType.BusinessConnection => update.BusinessConnection!.UserChatId,
            UpdateType.BusinessMessage  => update.BusinessMessage!.Chat.Id,
            UpdateType.DeletedBusinessMessages => update.DeletedBusinessMessages!.Chat.Id,
            UpdateType.EditedBusinessMessage => update.EditedBusinessMessage!.Chat.Id,

            UpdateType.CallbackQuery => update.CallbackQuery!.Message!.Chat.Id,

            UpdateType.ChannelPost => update.ChannelPost!.Chat.Id,
            UpdateType.EditedChannelPost => update.EditedChannelPost!.Chat.Id,

            UpdateType.ChatBoost => update.ChatBoost!.Chat.Id,
            UpdateType.RemovedChatBoost => update.RemovedChatBoost!.Chat.Id,

            UpdateType.ChatMember => update.ChatMember!.Chat.Id,
            UpdateType.ChatJoinRequest => update.ChatJoinRequest!.Chat.Id,
            UpdateType.MyChatMember => update.MyChatMember!.Chat.Id,

            UpdateType.ChosenInlineResult => update.ChosenInlineResult!.From.Id,
            UpdateType.InlineQuery => update.InlineQuery!.From.Id,

            UpdateType.Message => update.Message!.Chat.Id,
            UpdateType.EditedMessage => update.EditedMessage!.Chat.Id,

            UpdateType.MessageReaction => update.MessageReaction!.Chat.Id,
            UpdateType.MessageReactionCount => update.MessageReactionCount!.Chat.Id,

            UpdateType.Poll => long.Parse(update.Poll!.Id),
            UpdateType.PollAnswer => long.Parse(update.PollAnswer!.PollId),

            UpdateType.PreCheckoutQuery => update.PreCheckoutQuery!.From.Id,
            UpdateType.PurchasedPaidMedia =>  update.PurchasedPaidMedia!.From.Id,
            UpdateType.ShippingQuery => update.ShippingQuery!.From.Id,

            UpdateType.Unknown => 0,
            _ => 0
        };
    }

    private static UpdateSource ExtractUpdateSource(Update update)
    {
        return update.Type switch
        {
            UpdateType.BusinessConnection
                or UpdateType.BusinessMessage
                or UpdateType.DeletedBusinessMessages
                or UpdateType.EditedBusinessMessage => UpdateSource.BusinessAccount,

            UpdateType.CallbackQuery => GetUpdateSourceFromChatType(update.CallbackQuery!.Message!.Chat.Type),

            UpdateType.ChannelPost
                or UpdateType.EditedChannelPost => UpdateSource.Channel,

            UpdateType.ChatBoost => GetUpdateSourceFromChatType(update.ChatBoost!.Chat.Type),
            UpdateType.RemovedChatBoost => GetUpdateSourceFromChatType(update.RemovedChatBoost!.Chat.Type),

            UpdateType.ChatMember => GetUpdateSourceFromChatType(update.ChatMember!.Chat.Type),
            UpdateType.ChatJoinRequest => GetUpdateSourceFromChatType(update.ChatJoinRequest!.Chat.Type),
            UpdateType.MyChatMember => GetUpdateSourceFromChatType(update.MyChatMember!.Chat.Type),

            UpdateType.ChosenInlineResult
                or UpdateType.InlineQuery => UpdateSource.InlineMode,

            UpdateType.Message => GetUpdateSourceFromChatType(update.Message!.Chat.Type),
            UpdateType.EditedMessage => GetUpdateSourceFromChatType(update.EditedMessage!.Chat.Type),

            UpdateType.MessageReaction => GetUpdateSourceFromChatType(update.MessageReaction!.Chat.Type),
            UpdateType.MessageReactionCount => GetUpdateSourceFromChatType(update.MessageReactionCount!.Chat.Type),

            UpdateType.Poll
                or UpdateType.PollAnswer => UpdateSource.Poll,

            UpdateType.PreCheckoutQuery
                or UpdateType.PurchasedPaidMedia
                or UpdateType.ShippingQuery => UpdateSource.Payment,

            UpdateType.Unknown => UpdateSource.Unknown,
            _ => UpdateSource.Unknown
        };
    }
    
    private static UpdateSource GetUpdateSourceFromChatType(ChatType chatType)
    {
        return chatType switch
        {
            ChatType.Group => UpdateSource.Group,
            ChatType.Supergroup => UpdateSource.SuperGroup,
            ChatType.Private => UpdateSource.PrivateChat,
            ChatType.Channel => UpdateSource.Channel,
            _ => default
        };
    }
}
