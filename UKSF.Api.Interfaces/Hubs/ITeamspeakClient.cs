using System.Threading.Tasks;
using UKSF.Api.Models.Integrations;

namespace UKSF.Api.Interfaces.Hubs {
    public interface ITeamspeakClient {
        Task Receive(TeamspeakProcedureType procedure, object args);
    }
}
