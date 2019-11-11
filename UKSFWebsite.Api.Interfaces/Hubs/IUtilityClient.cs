using System.Threading.Tasks;

namespace UKSFWebsite.Api.Interfaces.Hubs {
    public interface IUtilityClient {
        Task ReceiveFrontendUpdate(string version);
    }
}
