using System.Threading.Tasks;

namespace UKSF.Api.Personnel.Signalr.Clients {
    public interface IAccountClient {
        Task ReceiveAccountUpdate();
    }
}