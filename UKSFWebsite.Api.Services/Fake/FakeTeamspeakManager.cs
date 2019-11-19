using UKSFWebsite.Api.Interfaces.Integrations.Teamspeak;
using UKSFWebsite.Api.Models.Integrations;

namespace UKSFWebsite.Api.Services.Fake {
    public class FakeTeamspeakManager : ITeamspeakManager {
        public void Start() { }

        public void SendProcedure(TeamspeakProcedureType procedure, object args) { }
    }
}
