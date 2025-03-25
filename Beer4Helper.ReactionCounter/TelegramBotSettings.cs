namespace Beer4Helper.ReactionCounter;

public class TelegramBotSettings
{
    public long[] AllowedChatIds { get; init; } = [];
    public long[] CommandChatIds { get; init; } = [];
}