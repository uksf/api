using System.Threading.Tasks;

namespace UKSF.Api.ArmaServer.Signalr.Clients {
    public interface IServersClient {
        Task ReceiveDisabledState(bool state);
    }
}
