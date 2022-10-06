using UKSF.Api.Teamspeak.Models;

namespace UKSF.Api.Teamspeak.Signalr.Clients;

public interface ITeamspeakClient
{
    Task Receive(TeamspeakProcedureType procedure, object args);
}
