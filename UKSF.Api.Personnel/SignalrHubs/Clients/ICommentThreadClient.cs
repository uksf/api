using System.Threading.Tasks;

namespace UKSF.Api.Personnel.SignalrHubs.Clients {
    public interface ICommentThreadClient {
        Task ReceiveComment(object comment);
        Task DeleteComment(string id);
    }
}
