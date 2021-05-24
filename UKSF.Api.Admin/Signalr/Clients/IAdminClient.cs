using System.Threading.Tasks;

namespace UKSF.Api.Admin.Signalr.Clients
{
    public interface IAdminClient
    {
        Task ReceiveLog();
    }
}
