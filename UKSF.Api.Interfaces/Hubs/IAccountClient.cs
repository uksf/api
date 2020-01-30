using System.Threading.Tasks;

namespace UKSF.Api.Interfaces.Hubs {
    public interface IAccountClient {
        Task ReceiveAccountUpdate();
    }
}
