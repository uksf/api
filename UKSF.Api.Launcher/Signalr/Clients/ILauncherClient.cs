using System.Threading.Tasks;

namespace UKSF.Api.Launcher.Signalr.Clients {
    public interface ILauncherClient {
        Task ReceiveLauncherVersion(string version);
    }
}
