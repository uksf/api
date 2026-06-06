using System.Net.Http;
using System.Text;
using UKSF.Api.Core;

namespace UKSF.Api.ArmaServer.Npc.Services;

public interface IGameServerCommandSender
{
    Task SendCommandAsync(int apiPort, string sqfArray);
}

public class GameServerCommandSender(IHttpClientFactory httpClientFactory, IUksfLogger logger) : IGameServerCommandSender
{
    public async Task SendCommandAsync(int apiPort, string sqfArray)
    {
        try
        {
            using var client = httpClientFactory.CreateClient();
            var content = new StringContent(sqfArray, Encoding.UTF8, "text/plain");
            var response = await client.PostAsync($"http://localhost:{apiPort}/command", content);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning($"NPC command push to game server port {apiPort} returned {(int)response.StatusCode}");
            }
        }
        catch (Exception exception)
        {
            // Fire-and-forget: swallow connection/timeout errors, log so drops are visible.
            logger.LogWarning($"Failed to push NPC command to game server port {apiPort}: {exception.Message}");
        }
    }
}
