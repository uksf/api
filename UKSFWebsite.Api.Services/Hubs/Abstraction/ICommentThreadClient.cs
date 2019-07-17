using System.Threading.Tasks;

namespace UKSFWebsite.Api.Services.Hubs.Abstraction {
    public interface ICommentThreadClient {
        Task ReceiveComment(object comment);
        Task DeleteComment(int index);
    }
}
