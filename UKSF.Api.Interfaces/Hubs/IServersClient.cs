using System.Threading.Tasks;

namespace UKSF.Api.Interfaces.Hubs {
    public interface IServersClient {
        Task ReceiveDisabledState(bool state);
    }
}
