using System.Threading.Tasks;
using UKSFWebsite.Api.Models.Integrations;

namespace UKSFWebsite.Api.Interfaces.Hubs {
    public interface ITeamspeakClient {
        Task Receive(TeamspeakProcedureType procedure, object args);
    }
}
