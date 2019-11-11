using System.Threading.Tasks;

namespace UKSFWebsite.Api.Interfaces.Hubs {
    public interface ICommentThreadClient {
        Task ReceiveComment(object comment);
        Task DeleteComment(string id);
    }
}
