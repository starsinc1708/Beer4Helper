using Beer4Helper.Shared;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Beer4Helper.PollingService.Services;

public class ModuleService(
    HttpClient httpClient,
    TgBotSettings settings,
    ILogger<ModuleService> logger)
{
    public async Task SendUpdateToSuitableModules(Update update, UpdateType type, UpdateSource source, long fromId,
        CancellationToken ct)
    {
        if (settings.BotModules != null)
        {
            var suitableModules = settings.BotModules
                .Select(m => m.Value)
                .Where(m => 
                    m.ParsedAllowedChats != null && 
                    m.ParsedAllowedChats.TryGetValue(source, out var allowedChats) && 
                    (allowedChats.Contains(fromId.ToString()) || allowedChats.Contains("All")))
                .ToList();

            if (suitableModules.Count == 0)
            {
                logger.LogInformation($"[{source}-{type} FROM {fromId}] - No suitable modules found");
                return;
            }
            
            suitableModules = suitableModules
                .Where(m => m.ParsedAllowedUpdates != null && m.ParsedAllowedUpdates[source].Contains(type)).ToList();
                
            if (suitableModules.Count == 0)
            {
                logger.LogInformation($"[{source}-{type} FROM {fromId}] - No suitable modules found");
                return;
            }
            
            foreach (var module in suitableModules)
            {
                await SendUpdate(module.In!, update, source, type, ct);
            }
        }
    }
    
    private async Task SendUpdate(InputSettings input, Update update, UpdateSource source, UpdateType updateType,  CancellationToken ct)
    {
        var host = input.Host;
        var port = input.Port;
        var endpoint = input.Endpoint;
        var type = input.Type;
        logger.LogInformation($"Sending update to {type}://{host}:{port}{endpoint}");

        if (type is "http" or "https")
        {
            try
            {
                var url = $"{type}://{host}:{port}{endpoint}";
            
                var tgUpdateRequest = new TgUpdateRequest
                {
                    Update = update,
                    Source = source.ToString(),
                    Type = updateType.ToString(),
                    Token = settings.Token
                };

                var content = JsonContent.Create(tgUpdateRequest, options: JsonBotAPI.Options);

                var response = await httpClient.PostAsync(url, content, ct);
                if (response.IsSuccessStatusCode) return;

                logger.LogError("HTTP module responded with error: {StatusCode}", response.StatusCode);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send HTTP update to {Host}:{Port}", host, port);
            }
        }
    }
}