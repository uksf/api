using System.Threading.Tasks;

namespace UKSFWebsite.Api.Interfaces.Hubs {
    public interface IAccountClient {
        Task ReceiveAccountUpdate();
    }
}
