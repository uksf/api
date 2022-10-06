namespace UKSF.Api.Signalr.Clients;

public interface ICommentThreadClient
{
    Task ReceiveComment(object comment);
    Task DeleteComment(string id);
}
