using System.Collections.Generic;
using System.Threading.Tasks;
using Octokit;
using UKSF.Api.Models.Integrations.Github;
using UKSF.Api.Models.Modpack;

namespace UKSF.Api.Interfaces.Integrations.Github {
    public interface IGithubService {
        Task<List<string>> GetBranches();
        Task<List<ModpackRelease>> GetHistoricReleases();
        Task<string> GetReferenceVersion(string reference);
        Task<GithubCommit> GetLatestReferenceCommit(string reference);
        Task<GithubCommit> GetPushEvent(PushWebhookPayload payload, string latestCommit = "");
        bool VerifySignature(string signature, string body);
        Task<bool> IsReferenceValid(string reference);
        Task<string> GenerateChangelog(string version);
        Task PublishRelease(ModpackRelease release);
        Task<Merge> MergeBranch(string branch, string sourceBranch, string version);
    }
}
