using System.Threading.Tasks;

namespace UKSF.Api.Interfaces.Command {
    public interface ICommandRequestCompletionService {
        Task Resolve(string id);
    }
}
