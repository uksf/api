namespace UKSF.Api.Shared.Signalr.Clients;

public interface INotificationsClient
{
    Task ReceiveNotification(object notification);
    Task ReceiveRead(IEnumerable<string> ids);
    Task ReceiveClear(IEnumerable<string> ids);
}
