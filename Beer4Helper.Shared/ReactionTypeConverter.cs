using Newtonsoft.Json.Linq;

namespace Beer4Helper.Shared;

using Newtonsoft.Json;
using Telegram.Bot.Types;

public class ReactionTypeConverter : JsonConverter<ReactionType>
{
    public override ReactionType? ReadJson(JsonReader reader, Type objectType, ReactionType? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        var jsonObject = JObject.Load(reader);
        
        Console.WriteLine(jsonObject.ToString());

        var reactionTypeString = string.Empty;

        // Attempt to read 'Type' as an integer
        var reactionTypeInt = jsonObject["Type"]?.Value<int>();
        if (reactionTypeInt.HasValue)
        {
            reactionTypeString = reactionTypeInt.Value.ToString();
        }
        
        // Based on the 'Type', manually deserialize the correct object to avoid recursion
        if (reactionTypeString == "1")
        {
            // Deserialize the JSON object to ReactionTypeEmoji (no recursion)
            return jsonObject.ToObject<ReactionTypeEmoji>(serializer);
        }
        else if (reactionTypeString == "2")
        {
            // Deserialize the JSON object to ReactionTypeCustomEmoji (no recursion)
            return jsonObject.ToObject<ReactionTypeCustomEmoji>(serializer);
        }
        else
        {
            // If we encounter an unknown type, throw an exception
            throw new JsonSerializationException($"Unknown reaction type: {reactionTypeString}");
        }
    }

    public override void WriteJson(JsonWriter writer, ReactionType? value, JsonSerializer serializer)
    {
        // Write the value back as a JSON object
        if (value != null)
        {
            JObject.FromObject(value, serializer).WriteTo(writer);
        }
    }
}

