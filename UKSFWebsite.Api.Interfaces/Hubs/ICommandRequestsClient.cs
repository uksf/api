using System.Threading.Tasks;

namespace UKSFWebsite.Api.Interfaces.Hubs {
    public interface ICommandRequestsClient {
        Task ReceiveRequestUpdate();
    }
}
