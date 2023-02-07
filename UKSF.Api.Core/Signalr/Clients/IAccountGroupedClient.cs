namespace UKSF.Api.Core.Signalr.Clients;

public interface IAccountGroupedClient
{
    Task ReceiveAccountUpdate();
}
