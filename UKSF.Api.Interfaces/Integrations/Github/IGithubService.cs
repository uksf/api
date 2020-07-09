using System.Collections.Generic;
using System.Threading.Tasks;
using Octokit;
using UKSF.Api.Models.Integrations.Github;
using UKSF.Api.Models.Modpack;

namespace UKSF.Api.Interfaces.Integrations.Github {
    public interface IGithubService {
        bool VerifySignature(string signature, string body);
        Task<string> GetReferenceVersion(string reference);
        Task<bool> IsReferenceValid(string reference);
        Task<GithubCommit> GetLatestReferenceCommit(string reference);
        Task<Merge> MergeBranch(string branch, string sourceBranch, string version);
        Task<GithubCommit> GetPushEvent(PushWebhookPayload payload, string latestCommit = "");
        Task<string> GenerateChangelog(string version);
        Task<List<ModpackRelease>> GetHistoricReleases();
        Task PublishRelease(ModpackRelease release);
        Task<List<string>> GetBranches();
    }
}
