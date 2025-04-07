namespace Beer4Helper.Shared;

public class TelegramBotSettings
{
    public string? Token { get; init; }
    public long EventPollChatId { get; init; }
    public long[] ReactionChatIds { get; init; } = [];
    public long[] CommandChatIds { get; init; } = [];
}