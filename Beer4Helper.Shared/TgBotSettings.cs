using System.Globalization;
using Telegram.Bot.Types.Enums;

namespace Beer4Helper.Shared;

public class TgBotSettings
{
    public Dictionary<string, BotModule>? BotModules { get; set; }
    public string? Token { get; set; }
}

public class BotModule
{
    public InputSettings? In { get; set; }
    
    private Dictionary<string, List<string>>? _allowedUpdates;
    public Dictionary<string, List<string>>? AllowedUpdates
    {
        get => _allowedUpdates;
        set
        {
            _allowedUpdates = ConvertDictionaryValuesToPascalCase(value);
            ParsedAllowedUpdates = ParseAllowedUpdates(_allowedUpdates);
        }
    }
    
    private Dictionary<string, List<string>>? _allowedChats;
    public Dictionary<string, List<string>>? AllowedChats
    {
        get => _allowedChats;
        set
        {
            _allowedChats = ConvertDictionaryValuesToPascalCase(value);
            ParsedAllowedChats = ParseAllowedChats(_allowedChats);
        }
    }

    private static Dictionary<UpdateSource, List<string>> ParseAllowedChats(Dictionary<string, List<string>>? allowedChats)
    {
        var chats = new Dictionary<UpdateSource, List<string>>();

        if (allowedChats == null)
            return chats;

        foreach (var (key, chatIds) in allowedChats)
        {
            var sourceArr = key.Split(',');

            foreach (var sourceStr in sourceArr)
            {
                var source = Enum.Parse<UpdateSource>(sourceStr.Trim(), ignoreCase: true);

                if (chats.TryGetValue(source, out var list))
                {
                    list.AddRange(chatIds);
                }
                else
                {
                    chats[source] = chatIds;
                }
            }
        }

        return chats;
    }

    public Dictionary<UpdateSource,  List<UpdateType>>? ParsedAllowedUpdates { get; private set; }
    public Dictionary<UpdateSource,  List<string>>? ParsedAllowedChats { get; private set; }

    private static Dictionary<string, List<string>>? ConvertDictionaryValuesToPascalCase(Dictionary<string, List<string>>? input)
    {
        if (input == null)
            return null;

        var result = new Dictionary<string, List<string>>();
            
        foreach (var kvp in input)
        {
            var convertedValues = kvp.Value
                .Select(ToPascalCase)
                .ToList();

            result.Add(kvp.Key, convertedValues);
        }
        
        return result;
    }

    private Dictionary<UpdateSource, List<UpdateType>> ParseAllowedUpdates(Dictionary<string, List<string>>? allowedUpdates)
    {
        var updates = new Dictionary<UpdateSource, List<UpdateType>>();

        if (allowedUpdates == null)
            return updates;

        foreach (var (key, updateNames) in allowedUpdates)
        {
            var sourceArr = key.Split(',');

            foreach (var sourceStr in sourceArr)
            {
                var source = Enum.Parse<UpdateSource>(sourceStr.Trim(), ignoreCase: true);
                var parsedUpdates = updateNames
                    .Select(u => Enum.Parse<UpdateType>(u, ignoreCase: true))
                    .ToList();

                if (updates.TryGetValue(source, out var list))
                {
                    list.AddRange(parsedUpdates);
                }
                else
                {
                    updates[source] = parsedUpdates;
                }
            }
        }

        return updates;
    }

    private static string ToPascalCase(string input)
    {
        return string.Join("", input.Split('_')
            .Select(word => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(word)));
    }
}

public class InputSettings
{
    public string? Type { get; set; }
    
    public string? Host { get; set; }
    
    public int Port { get; set; }
    
    public string? Endpoint { get; set; }
}

