using System.Threading.Tasks;

namespace UKSF.Api.Command.Signalr.Clients
{
    public interface ICommandRequestsClient
    {
        Task ReceiveRequestUpdate();
    }
}
