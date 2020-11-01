using System.Threading.Tasks;

namespace UKSF.Api.Personnel.SignalrHubs.Clients {
    public interface IAccountClient {
        Task ReceiveAccountUpdate();
    }
}
