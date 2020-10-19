using System.Threading.Tasks;
using UKSF.Api.Interfaces.Integrations.Teamspeak;
using UKSF.Api.Models.Integrations;

namespace UKSF.Api.Services.Fake {
    public class FakeTeamspeakManagerService : ITeamspeakManagerService {
        public void Start() { }

        public void Stop() { }

        public Task SendGroupProcedure(TeamspeakProcedureType procedure, TeamspeakGroupProcedure groupProcedure) => Task.CompletedTask;

        public Task SendProcedure(TeamspeakProcedureType procedure, object args) => Task.CompletedTask;
    }
}
