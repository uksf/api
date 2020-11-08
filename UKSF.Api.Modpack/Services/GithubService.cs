using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GitHubJwt;
using Microsoft.Extensions.Configuration;
using Octokit;
using UKSF.Api.Base.Events;
using UKSF.Api.Modpack.Models;

namespace UKSF.Api.Modpack.Services {
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
    }

    public class GithubService : IGithubService {
        private const string REPO_ORG = "uksf";
        private const string REPO_NAME = "modpack";
        private const string VERSION_FILE = "addons/main/script_version.hpp";
        private const int APP_ID = 53456;
        private const long APP_INSTALLATION = 6681715;
        private const string APP_NAME = "uksf-api-integration";
        private static readonly string[] LABELS_ADDED = { "type/feature", "type/mod addition" };
        private static readonly string[] LABELS_CHANGED = { "type/arsenal", "type/cleanup", "type/enhancement", "type/task" };
        private static readonly string[] LABELS_FIXED = { "type/bug fix", "type/bug" };
        private static readonly string[] LABELS_UPDATED = { "type/mod update" };
        private static readonly string[] LABELS_REMOVED = { "type/mod deletion" };
        private static readonly string[] LABELS_EXCLUDE = { "type/cleanup", "type/by design", "fault/bi", "fault/other mod" };

        private readonly IConfiguration configuration;
        private readonly ILogger logger;

        public GithubService(IConfiguration configuration, ILogger logger) {
            this.configuration = configuration;
            this.logger = logger;
        }

        public bool VerifySignature(string signature, string body) {
            string secret = configuration.GetSection("Github")["webhookSecret"];
            byte[] data = Encoding.UTF8.GetBytes(body);
            byte[] secretData = Encoding.UTF8.GetBytes(secret);
            using HMACSHA1 hmac = new HMACSHA1(secretData);
            byte[] hash = hmac.ComputeHash(data);
            string sha1 = $"sha1={BitConverter.ToString(hash).ToLower().Replace("-", "")}";
            return string.Equals(sha1, signature);
        }

        public async Task<string> GetReferenceVersion(string reference) {
            reference = reference.Split('/')[^1];
            GitHubClient client = await GetAuthenticatedClient();
            byte[] contentBytes = await client.Repository.Content.GetRawContentByRef(REPO_ORG, REPO_NAME, VERSION_FILE, reference);
            if (contentBytes.Length == 0) {
                return "0.0.0";
            }

            string content = Encoding.UTF8.GetString(contentBytes);
            IEnumerable<string> lines = content.Split("\n").Take(3);
            string version = string.Join('.', lines.Select(x => x.Split(' ')[^1]));
            return version;
        }

        public async Task<bool> IsReferenceValid(string reference) {
            string version = await GetReferenceVersion(reference);
            int[] versionParts = version.Split('.').Select(int.Parse).ToArray();
            // Version when make.py was changed to accommodate this system
            return versionParts[0] == 5 ? versionParts[1] == 17 ? versionParts[2] >= 19 : versionParts[1] > 17 : versionParts[0] > 5;
        }

        public async Task<GithubCommit> GetLatestReferenceCommit(string reference) {
            GitHubClient client = await GetAuthenticatedClient();
            GitHubCommit commit = await client.Repository.Commit.Get(REPO_ORG, REPO_NAME, reference);
            string branch = Regex.Match(reference, @"^[a-fA-F0-9]{40}$").Success ? "None" : reference;
            return new GithubCommit { branch = branch, before = commit.Parents.FirstOrDefault()?.Sha, after = commit.Sha, message = commit.Commit.Message, author = commit.Commit.Author.Email };
        }

        public async Task<GithubCommit> GetPushEvent(PushWebhookPayload payload, string latestCommit = "") {
            if (string.IsNullOrEmpty(latestCommit)) {
                latestCommit = payload.Before;
            }

            GitHubClient client = await GetAuthenticatedClient();
            CompareResult result = await client.Repository.Commit.Compare(REPO_ORG, REPO_NAME, latestCommit, payload.After);
            string message = result.Commits.Count > 0 ? CombineCommitMessages(result.Commits) : result.BaseCommit.Commit.Message;
            return new GithubCommit { branch = payload.Ref, baseBranch = payload.BaseRef, before = payload.Before, after = payload.After, message = message, author = payload.HeadCommit.Author.Email };
        }

        public async Task<string> GenerateChangelog(string version) {
            GitHubClient client = await GetAuthenticatedClient();
            Milestone milestone = await GetOpenMilestone(version);
            if (milestone == null) {
                return "No milestone found";
            }

            IReadOnlyList<Issue> issues = await client.Issue.GetAllForRepository(
                REPO_ORG,
                REPO_NAME,
                new RepositoryIssueRequest { Milestone = milestone.Number.ToString(), State = ItemStateFilter.All }
            );

            string changelog = "";

            List<Issue> added = issues.Where(x => x.Labels.Any(y => LABELS_ADDED.Contains(y.Name) && !LABELS_EXCLUDE.Contains(y.Name))).OrderBy(x => x.Title).ToList();
            List<Issue> changed = issues.Where(x => x.Labels.Any(y => LABELS_CHANGED.Contains(y.Name) && !LABELS_EXCLUDE.Contains(y.Name))).OrderBy(x => x.Title).ToList();
            List<Issue> fixes = issues.Where(x => x.Labels.Any(y => LABELS_FIXED.Contains(y.Name) && !LABELS_EXCLUDE.Contains(y.Name))).OrderBy(x => x.Title).ToList();
            List<Issue> updated = issues.Where(x => x.Labels.Any(y => LABELS_UPDATED.Contains(y.Name) && !LABELS_EXCLUDE.Contains(y.Name))).OrderBy(x => x.Title).ToList();
            List<Issue> removed = issues.Where(x => x.Labels.Any(y => LABELS_REMOVED.Contains(y.Name) && !LABELS_EXCLUDE.Contains(y.Name))).OrderBy(x => x.Title).ToList();

            AddChangelogSection(ref changelog, added, "Added");
            AddChangelogSection(ref changelog, changed, "Changed");
            AddChangelogSection(ref changelog, fixes, "Fixed");
            AddChangelogUpdated(ref changelog, updated, "Updated");
            AddChangelogSection(ref changelog, removed, "Removed");

            return changelog;
        }

        public async Task PublishRelease(ModpackRelease release) {
            GitHubClient client = await GetAuthenticatedClient();

            try {
                await client.Repository.Release.Create(
                    REPO_ORG,
                    REPO_NAME,
                    new NewRelease(release.version) { Name = $"Modpack Version {release.version}", Body = $"{release.description}\n\n## Changelog\n{release.changelog.Replace("<br>", "\n")}" }
                );

                Milestone milestone = await GetOpenMilestone(release.version);
                if (milestone != null) {
                    await client.Issue.Milestone.Update(REPO_ORG, REPO_NAME, milestone.Number, new MilestoneUpdate { State = ItemState.Closed });
                }
            } catch (Exception exception) {
                logger.LogError(exception);
            }
        }

        public async Task<List<string>> GetBranches() {
            GitHubClient client = await GetAuthenticatedClient();
            IReadOnlyList<Branch> branches = await client.Repository.Branch.GetAll(REPO_ORG, REPO_NAME);
            ConcurrentBag<string> validBranchesBag = new ConcurrentBag<string>();
            IEnumerable<Task> task = branches.Select(
                async branch => {
                    if (await IsReferenceValid(branch.Name)) {
                        validBranchesBag.Add(branch.Name);
                    }
                }
            );
            await Task.WhenAll(task);

            List<string> validBranches = validBranchesBag.OrderBy(x => x).ToList();
            if (validBranches.Contains("release")) {
                validBranches.Remove("release");
                validBranches.Insert(0, "release");
            }

            if (validBranches.Contains("master")) {
                validBranches.Remove("master");
                validBranches.Insert(0, "master");
            }

            return validBranches;
        }

        public async Task<List<ModpackRelease>> GetHistoricReleases() {
            GitHubClient client = await GetAuthenticatedClient();

            IReadOnlyList<Release> releases = await client.Repository.Release.GetAll(REPO_ORG, "modpack");
            return releases.Select(x => new ModpackRelease { version = x.Name.Split(" ")[^1], timestamp = x.CreatedAt.DateTime, changelog = FormatChangelog(x.Body) }).ToList();
        }

        private static string CombineCommitMessages(IReadOnlyCollection<GitHubCommit> commits) {
            List<string> filteredCommitMessages = commits.Select(x => x.Commit.Message).Reverse().Where(x => !x.Contains("Merge branch") && !Regex.IsMatch(x, "Release \\d*\\.\\d*\\.\\d*")).ToList();
            return filteredCommitMessages.Count == 0 ? commits.First().Commit.Message : filteredCommitMessages.Aggregate((a, b) => $"{a}\n\n{b}");
        }

        private async Task<Milestone> GetOpenMilestone(string version) {
            GitHubClient client = await GetAuthenticatedClient();
            IReadOnlyList<Milestone> milestones = await client.Issue.Milestone.GetAllForRepository(REPO_ORG, REPO_NAME, new MilestoneRequest { State = ItemStateFilter.Open });
            Milestone milestone = milestones.FirstOrDefault(x => x.Title == version);
            if (milestone == null) {
                logger.LogWarning($"Could not find open milestone for version {version}");
            }

            return milestone;
        }

        private static void AddChangelogSection(ref string changelog, IReadOnlyCollection<Issue> issues, string header) {
            if (issues.Any()) {
                changelog += $"#### {header}";
                changelog += issues.Select(x => $"\n- {x.Title} [(#{x.Number})]({x.HtmlUrl})").Aggregate((a, b) => a + b);
                changelog += "\n\n";
            }
        }

        private static void AddChangelogUpdated(ref string changelog, IReadOnlyCollection<Issue> issues, string header) {
            if (issues.Any()) {
                changelog += $"#### {header}";
                changelog += issues.Select(
                                       x => {
                                           string[] titleParts = x.Title.Split(" ");
                                           return $"\n- {titleParts[0]} to [{titleParts[1]}]({x.HtmlUrl})";
                                       }
                                   )
                                   .Aggregate((a, b) => a + b);
                changelog += "\n\n";
            }
        }

        private static string FormatChangelog(string body) {
            string changelog = body.Replace("\r\n", "\n").Replace("\n[Report", "<br>[Report");
            string[] lines = changelog.Split("\n");
            for (int i = 0; i < lines.Length; i++) {
                string line = lines[i];

                if (line.StartsWith("  ") && !Regex.Match(line, @"( {2,})-").Success) {
                    lines[i] = $"<br>{line}";
                }

                Match match = Regex.Match(line, @"]\(#\d+\)");
                if (match.Success) {
                    string number = match.Value.Replace("]", "").Replace("(", "").Replace(")", "").Replace("#", "");
                    lines[i] = line.Replace(match.Value, $"](https://github.com/uksf/modpack/issues/{number})");
                } else {
                    match = Regex.Match(line, @"\(#\d+\)");
                    if (match.Success) {
                        string number = match.Value.Replace("(", "").Replace(")", "").Replace("#", "");
                        lines[i] = line.Replace(match.Value, $"[{match.Value}](https://github.com/uksf/modpack/issues/{number})");
                    }
                }
            }

            return string.Join("\n", lines);
        }

        private async Task<GitHubClient> GetAuthenticatedClient() {
            GitHubClient client = new GitHubClient(new ProductHeaderValue(APP_NAME)) { Credentials = new Credentials(GetJwtToken(), AuthenticationType.Bearer) };
            AccessToken response = await client.GitHubApps.CreateInstallationToken(APP_INSTALLATION);
            GitHubClient installationClient = new GitHubClient(new ProductHeaderValue(APP_NAME)) { Credentials = new Credentials(response.Token) };
            return installationClient;
        }

        private string GetJwtToken() {
            string privateKey = configuration.GetSection("Github")["appPrivateKey"].Replace("\n", Environment.NewLine, StringComparison.Ordinal);
            GitHubJwtFactory generator = new GitHubJwtFactory(new StringPrivateKeySource(privateKey), new GitHubJwtFactoryOptions { AppIntegrationId = APP_ID, ExpirationSeconds = 540 });
            return generator.CreateEncodedJwtToken();
        }
    }
}
