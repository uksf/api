namespace UKSF.Api.ArmaServer.Models;

public class GameServerEvent
{
    public string Type { get; set; } = string.Empty;
    public int ApiPort { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
}
