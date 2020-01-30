using System.Threading.Tasks;

namespace UKSF.Api.Interfaces.Hubs {
    public interface ICommandRequestsClient {
        Task ReceiveRequestUpdate();
    }
}
