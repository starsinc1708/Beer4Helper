namespace Beer4Helper.ReactionCounter;

public class TelegramBotSettings
{
    public long[] AllowedChatIds { get; set; } = [];
    public long[] CommandChatIds { get; set; } = [];
}