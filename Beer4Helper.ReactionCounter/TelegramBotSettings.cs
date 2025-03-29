namespace Beer4Helper.ReactionCounter;

public class TelegramBotSettings
{
    public long[] ReactionChatIds { get; init; } = [];
    public long[] CommandChatIds { get; init; } = [];
}