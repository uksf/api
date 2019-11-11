using System.Threading.Tasks;

namespace UKSFWebsite.Api.Interfaces.Command {
    public interface ICommandRequestCompletionService {
        Task Resolve(string id);
    }
}
