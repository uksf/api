namespace UKSF.Api.Core.Signalr.Clients;

public interface INotificationsClient
{
    Task ReceiveNotification(object notification);
    Task ReceiveRead(IEnumerable<string> ids);
    Task ReceiveClear(IEnumerable<string> ids);
}
