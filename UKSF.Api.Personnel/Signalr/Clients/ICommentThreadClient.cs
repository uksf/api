using System.Threading.Tasks;

namespace UKSF.Api.Personnel.Signalr.Clients
{
    public interface ICommentThreadClient
    {
        Task ReceiveComment(object comment);
        Task DeleteComment(string id);
    }
}
