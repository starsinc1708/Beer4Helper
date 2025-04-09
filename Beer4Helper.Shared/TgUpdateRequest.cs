using Telegram.Bot.Types;

namespace Beer4Helper.Shared;

public class TgUpdateRequest
{
    public Update? Update { get; init; }
    public string? Type { get; init; }
    public string? Source { get; set; }
    public string? Token { get; set; }
}