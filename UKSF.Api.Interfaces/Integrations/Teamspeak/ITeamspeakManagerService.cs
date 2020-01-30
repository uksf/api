using System.Threading.Tasks;
using UKSF.Api.Models.Integrations;

namespace UKSF.Api.Interfaces.Integrations.Teamspeak {
    public interface ITeamspeakManagerService {
        void Start();
        void Stop();
        Task SendProcedure(TeamspeakProcedureType procedure, object args);
    }
}
