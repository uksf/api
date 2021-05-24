using System.Threading.Tasks;

namespace UKSF.Api.Admin.Signalr.Clients
{
    public interface IUtilityClient
    {
        Task ReceiveFrontendUpdate(string version);
    }
}
