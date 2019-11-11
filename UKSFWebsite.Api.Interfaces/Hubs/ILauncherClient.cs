using System.Threading.Tasks;

namespace UKSFWebsite.Api.Interfaces.Hubs {
    public interface ILauncherClient {
        Task ReceiveLauncherVersion(string version);
    }
}
