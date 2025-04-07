using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Beer4Helper.Shared;

public class TgUpdateRequest
{
    public string? Type { get; set; }
    public string? Source { get; set; }
    public Update? Update { get; set; }
}