using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Beer4Helper.Shared;

public static class ConfigLoader
{
    public static TgBotSettings LoadConfig(string filePath)
    {
        var yaml = File.ReadAllText(filePath);

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(NullNamingConvention.Instance)
            .Build();

        var modules = deserializer.Deserialize<TgBotSettings>(yaml);
        return modules;
    }
}
