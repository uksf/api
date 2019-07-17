using System.Threading.Tasks;

namespace UKSFWebsite.Api.Services.Hubs.Abstraction {
    public interface IUtilityClient {
        Task ReceiveFrontendUpdate(string version);
    }
}
