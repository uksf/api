using System.Threading.Tasks;
using UKSFWebsite.Api.Models.Integrations;

namespace UKSFWebsite.Api.Interfaces.Integrations.Teamspeak {
    public interface ITeamspeakManagerService {
        void Start();
        void Stop();
        Task SendProcedure(TeamspeakProcedureType procedure, object args);
    }
}
