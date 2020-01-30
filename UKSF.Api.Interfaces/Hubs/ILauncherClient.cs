using System.Threading.Tasks;

namespace UKSF.Api.Interfaces.Hubs {
    public interface ILauncherClient {
        Task ReceiveLauncherVersion(string version);
    }
}
