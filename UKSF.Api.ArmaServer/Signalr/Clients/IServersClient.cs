using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.ArmaServer.Signalr.Clients;

public interface IServersClient
{
    Task ReceiveDisabledState(bool state);
    Task ReceiveServersUpdate(GameServersUpdate update);
    Task ReceiveServerUpdate(GameServerUpdate update);
    Task ReceiveMissionsUpdate(List<MissionFile> missions);
    Task ReceiveLogContent(string serverId, string source, List<string> lines, int startLineIndex, bool isComplete);
    Task ReceiveLogAppend(string serverId, string source, List<string> lines);
}
