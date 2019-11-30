using System.Threading.Tasks;

namespace UKSFWebsite.Api.Interfaces.Hubs {
    public interface IGameClientClient {
        Task ReceiveDisabledState(bool state);
    }
}
