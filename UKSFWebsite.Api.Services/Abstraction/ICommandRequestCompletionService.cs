using System.Threading.Tasks;

namespace UKSFWebsite.Api.Services.Abstraction {
    public interface ICommandRequestCompletionService {
        Task Resolve(string id);
    }
}
