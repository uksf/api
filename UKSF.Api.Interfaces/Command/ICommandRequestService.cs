using System.Threading.Tasks;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Models.Command;

namespace UKSF.Api.Interfaces.Command {
    public interface ICommandRequestService : IDataBackedService<ICommandRequestDataService> {
        Task Add(CommandRequest request, ChainOfCommandMode mode = ChainOfCommandMode.COMMANDER_AND_ONE_ABOVE);
        Task ArchiveRequest(string id);
        Task SetRequestReviewState(CommandRequest request, string reviewerId, ReviewState newState);
        Task SetRequestAllReviewStates(CommandRequest request, ReviewState newState);
        ReviewState GetReviewState(string id, string reviewer);
        bool IsRequestApproved(string id);
        bool IsRequestRejected(string id);
        bool DoesEquivalentRequestExist(CommandRequest request);
    }
}
