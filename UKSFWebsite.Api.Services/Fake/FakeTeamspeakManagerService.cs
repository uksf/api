using System.Threading.Tasks;
using UKSFWebsite.Api.Interfaces.Integrations.Teamspeak;
using UKSFWebsite.Api.Models.Integrations;

namespace UKSFWebsite.Api.Services.Fake {
    public class FakeTeamspeakManagerService : ITeamspeakManagerService {
        public void Start() { }

        public void Stop() { }

        public Task SendProcedure(TeamspeakProcedureType procedure, object args) => Task.CompletedTask;
    }
}
