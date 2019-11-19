using UKSFWebsite.Api.Models.Integrations;

namespace UKSFWebsite.Api.Interfaces.Integrations.Teamspeak {
    public interface ITeamspeakManager {
        void Start();
        void SendProcedure(TeamspeakProcedureType procedure, object args);
    }
}
