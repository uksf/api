using System.Threading.Tasks;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Models.Command;

namespace UKSFWebsite.Api.Interfaces.Command {
    public interface ICommandRequestService : IDataBackedService<ICommandRequestDataService> {
        Task Add(CommandRequest request, ChainOfCommandMode mode);
        Task ArchiveRequest(string id);
        Task SetRequestReviewState(CommandRequest request, string reviewerId, ReviewState newState);
        Task SetRequestAllReviewStates(CommandRequest request, ReviewState newState);
        ReviewState GetReviewState(string id, string reviewer);
        bool IsRequestApproved(string id);
        bool IsRequestRejected(string id);
        bool DoesEquivalentRequestExist(CommandRequest request);
    }
}
