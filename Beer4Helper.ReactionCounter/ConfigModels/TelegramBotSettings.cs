namespace Beer4Helper.ReactionCounter.ConfigModels;

public class TelegramBotSettings
{
    public long MainChatId { get; set; }
    public long[] ReactionChatIds { get; init; } = [];
    public long[] CommandChatIds { get; init; } = [];
}