namespace UKSF.Api.Command.Signalr.Clients;

public interface ICommandRequestsClient
{
    Task ReceiveRequestUpdate();
}
