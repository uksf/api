using System.Threading.Tasks;

namespace UKSF.Api.Interfaces.Hubs {
    public interface IUtilityClient {
        Task ReceiveFrontendUpdate(string version);
    }
}
