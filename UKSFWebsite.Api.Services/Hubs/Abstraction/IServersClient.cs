using System.Threading.Tasks;

namespace UKSFWebsite.Api.Services.Hubs.Abstraction {
    public interface IServersClient {
        Task ReceiveDisabledState(bool state);
    }
}
