namespace UKSF.Api.ArmaServer.Models;

public class GameServerEvent
{
    public string Type { get; set; } = string.Empty;
    public Dictionary<string, object> Data { get; set; } = new();
}
