using System.Threading.Tasks;

namespace UKSFWebsite.Api.Services.Hubs.Abstraction {
    public interface ILauncherClient {
        Task ReceiveLauncherVersion(string version);
    }
}
