using System.Text.Json.Serialization;
using Telegram.Bot.Serialization;

namespace Beer4Helper.Shared;

[JsonConverter(typeof(EnumConverter<UpdateSource>))]
public enum UpdateSource
{
    PrivateChat,
    Channel,
    Group,
    SuperGroup,
    BusinessAccount,
    InlineMode,
    Payment,
    Poll,
    Unknown,
    Test
}