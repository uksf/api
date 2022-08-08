using System.Threading.Tasks;

namespace UKSF.Api.Shared.Signalr.Clients;

public interface IAllClient
{
    Task ReceiveAccountUpdate();
}
