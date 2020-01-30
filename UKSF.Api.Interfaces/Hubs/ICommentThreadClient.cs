using System.Threading.Tasks;

namespace UKSF.Api.Interfaces.Hubs {
    public interface ICommentThreadClient {
        Task ReceiveComment(object comment);
        Task DeleteComment(string id);
    }
}
