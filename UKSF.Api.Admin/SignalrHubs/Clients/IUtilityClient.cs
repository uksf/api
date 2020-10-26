using System.Threading.Tasks;

namespace UKSF.Api.Admin.SignalrHubs.Clients {
    public interface IUtilityClient {
        Task ReceiveFrontendUpdate(string version);
    }
}
