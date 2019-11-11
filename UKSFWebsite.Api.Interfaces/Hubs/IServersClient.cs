using System.Threading.Tasks;

namespace UKSFWebsite.Api.Interfaces.Hubs {
    public interface IServersClient {
        Task ReceiveDisabledState(bool state);
    }
}
