using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Beer4Helper.PollingService.Config;

public class ConfigLoader
{
    public static BotModuleSettings LoadConfig(string filePath)
    {
        var yaml = File.ReadAllText(filePath);

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(NullNamingConvention.Instance)
            .Build();

        var modules = deserializer.Deserialize<BotModuleSettings>(yaml);
        return modules;
    }
}
