using UKSF.Api.Integrations.Teamspeak.Models;

namespace UKSF.Api.Integrations.Teamspeak.Signalr.Clients;

public interface ITeamspeakClient
{
    Task Receive(TeamspeakProcedureType procedure, object args);
}
