using System.Threading.Tasks;

namespace UKSFWebsite.Api.Services.Hubs.Abstraction {
    public interface ICommandRequestsClient {
        Task ReceiveRequestUpdate();
    }
}
