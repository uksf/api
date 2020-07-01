using System.Threading.Tasks;

namespace UKSF.Api.Interfaces.Integrations.Github {
    public interface IGithubService {
        bool VerifySignature(string signature, string body);
        Task<string> GetCommitVersion(string branch);
    }
}
