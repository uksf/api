using System.Threading.Tasks;

namespace UKSF.Api.Shared.Signalr.Clients
{
    public interface IAccountGroupedClient
    {
        Task ReceiveAccountUpdate();
    }
}
