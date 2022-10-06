namespace UKSF.Api.Signalr.Clients;

public interface ICommandRequestsClient
{
    Task ReceiveRequestUpdate();
}
