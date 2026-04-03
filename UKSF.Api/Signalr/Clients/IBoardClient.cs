namespace UKSF.Api.Signalr.Clients;

public interface IBoardClient
{
    Task ReceiveCardMoved(object data);
    Task ReceiveCardCreated(object data);
    Task ReceiveCardUpdated(object data);
    Task ReceiveCardDeleted(object data);
    Task ReceiveBoardUpdated(object data);
}
