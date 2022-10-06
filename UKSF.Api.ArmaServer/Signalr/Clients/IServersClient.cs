using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.ArmaServer.Signalr.Clients;

public interface IServersClient
{
    Task ReceiveDisabledState(bool state);
    Task ReceiveAnyUpdateIfNotCaller(string connectionId, bool skipRefresh);
    Task ReceiveServerUpdateIfNotCaller(string connectionId, string serverId);
    Task ReceiveMissionsUpdateIfNotCaller(string connectionId, List<MissionFile> missions);
}
