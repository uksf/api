﻿using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using GitHubJwt;
using Microsoft.Extensions.Options;
using Octokit;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Configuration;

namespace UKSF.Api.Modpack.Services;

public interface IGithubService
{
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

public class GithubService : IGithubService
{
    private const string RepoOrg = "uksf";
    private const string RepoName = "modpack";
    private const string VersionFile = "addons/main/script_version.hpp";
    private const int AppId = 53456;
    private const long AppInstallation = 6681715;
    private const string AppName = "uksf-api-integration";
    private static readonly string[] LabelsAdded = { "type/feature", "type/mod addition" };
    private static readonly string[] LabelsChanged = { "type/arsenal", "type/cleanup", "type/enhancement", "type/task" };
    private static readonly string[] LabelsFixed = { "type/bug fix", "type/bug" };
    private static readonly string[] LabelsUpdated = { "type/mod update" };
    private static readonly string[] LabelsRemoved = { "type/mod deletion" };
    private static readonly string[] LabelsExclude = { "type/cleanup", "type/by design", "fault/bi", "fault/other mod" };
    private readonly string _appPrivateKey;

    private readonly IUksfLogger _logger;
    private readonly string _webhookSecret;

    public GithubService(IUksfLogger logger, IOptions<AppSettings> options)
    {
        _logger = logger;

        var appSettings = options.Value;
        _webhookSecret = appSettings.Secrets.Github.WebhookSecret;
        _appPrivateKey = appSettings.Secrets.Github.AppPrivateKey;
    }

    public bool VerifySignature(string signature, string body)
    {
        var data = Encoding.UTF8.GetBytes(body);
        var secretData = Encoding.UTF8.GetBytes(_webhookSecret);
        using HMACSHA1 hmac = new(secretData);
        var hash = hmac.ComputeHash(data);
        var sha1 = $"sha1={BitConverter.ToString(hash).ToLower().Replace("-", "")}";
        return string.Equals(sha1, signature);
    }

    public async Task<string> GetReferenceVersion(string reference)
    {
        var client = await GetAuthenticatedClient();
        var contentBytes = await client.Repository.Content.GetRawContentByRef(RepoOrg, RepoName, VersionFile, reference);
        if (contentBytes.Length == 0)
        {
            return "0.0.0";
        }

        var content = Encoding.UTF8.GetString(contentBytes);
        var lines = content.Split("\n").Take(3);
        var version = string.Join('.', lines.Select(x => x.Split(' ')[^1]));
        return version;
    }

    public async Task<bool> IsReferenceValid(string reference)
    {
        var version = await GetReferenceVersion(reference);
        var versionParts = version.Split('.').Select(int.Parse).ToArray();
        // Version when make.py was changed to accommodate this system
        return versionParts[0] == 5 ? versionParts[1] == 17 ? versionParts[2] >= 19 : versionParts[1] > 17 : versionParts[0] > 5;
    }

    public async Task<GithubCommit> GetLatestReferenceCommit(string reference)
    {
        var client = await GetAuthenticatedClient();
        var commit = await client.Repository.Commit.Get(RepoOrg, RepoName, reference);
        var branch = Regex.Match(reference, @"^[a-fA-F0-9]{40}$").Success ? "None" : reference;
        return new()
        {
            Branch = branch,
            Before = commit.Parents.FirstOrDefault()?.Sha,
            After = commit.Sha,
            Message = commit.Commit.Message,
            Author = commit.Commit.Author.Email
        };
    }

    public async Task<GithubCommit> GetPushEvent(PushWebhookPayload payload, string latestCommit = "")
    {
        if (string.IsNullOrEmpty(latestCommit))
        {
            latestCommit = payload.Before;
        }

        var client = await GetAuthenticatedClient();
        var result = await client.Repository.Commit.Compare(RepoOrg, RepoName, latestCommit, payload.After);
        var message = result.Commits.Count > 0 ? CombineCommitMessages(result.Commits) : result.BaseCommit.Commit.Message;
        return new()
        {
            Branch = payload.Ref,
            BaseBranch = payload.BaseRef,
            Before = payload.Before,
            After = payload.After,
            Message = message,
            Author = payload.HeadCommit.Author.Email
        };
    }

    public async Task<string> GenerateChangelog(string version)
    {
        var client = await GetAuthenticatedClient();
        var milestone = await GetOpenMilestone(version);
        if (milestone == null)
        {
            return "No milestone found";
        }

        var issues = await client.Issue.GetAllForRepository(
            RepoOrg,
            RepoName,
            new RepositoryIssueRequest { Milestone = milestone.Number.ToString(), State = ItemStateFilter.All }
        );

        var changelog = "";

        var added = issues.Where(x => x.Labels.Any(y => LabelsAdded.Contains(y.Name) && !LabelsExclude.Contains(y.Name))).OrderBy(x => x.Title).ToList();
        var changed = issues.Where(x => x.Labels.Any(y => LabelsChanged.Contains(y.Name) && !LabelsExclude.Contains(y.Name))).OrderBy(x => x.Title).ToList();
        var fixes = issues.Where(x => x.Labels.Any(y => LabelsFixed.Contains(y.Name) && !LabelsExclude.Contains(y.Name))).OrderBy(x => x.Title).ToList();
        var updated = issues.Where(x => x.Labels.Any(y => LabelsUpdated.Contains(y.Name) && !LabelsExclude.Contains(y.Name))).OrderBy(x => x.Title).ToList();
        var removed = issues.Where(x => x.Labels.Any(y => LabelsRemoved.Contains(y.Name) && !LabelsExclude.Contains(y.Name))).OrderBy(x => x.Title).ToList();

        AddChangelogSection(ref changelog, added, "Added");
        AddChangelogSection(ref changelog, changed, "Changed");
        AddChangelogSection(ref changelog, fixes, "Fixed");
        AddChangelogUpdated(ref changelog, updated, "Updated");
        AddChangelogSection(ref changelog, removed, "Removed");

        return changelog;
    }

    public async Task PublishRelease(ModpackRelease release)
    {
        var client = await GetAuthenticatedClient();

        try
        {
            await client.Repository.Release.Create(
                RepoOrg,
                RepoName,
                new(release.Version)
                {
                    Name = $"Modpack Version {release.Version}", Body = $"{release.Description}\n\n## Changelog\n{release.Changelog.Replace("<br>", "\n")}"
                }
            );

            var milestone = await GetOpenMilestone(release.Version);
            if (milestone != null)
            {
                await client.Issue.Milestone.Update(RepoOrg, RepoName, milestone.Number, new() { State = ItemState.Closed });
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception);
        }
    }

    public async Task<List<string>> GetBranches()
    {
        var client = await GetAuthenticatedClient();
        var branches = await client.Repository.Branch.GetAll(RepoOrg, RepoName);
        ConcurrentBag<string> validBranchesBag = new();
        var task = branches.Select(
            async branch =>
            {
                if (await IsReferenceValid(branch.Name))
                {
                    validBranchesBag.Add(branch.Name);
                }
            }
        );
        await Task.WhenAll(task);

        var validBranches = validBranchesBag.OrderBy(x => x).ToList();
        if (validBranches.Contains("release"))
        {
            validBranches.Remove("release");
            validBranches.Insert(0, "release");
        }

        if (validBranches.Contains("main"))
        {
            validBranches.Remove("main");
            validBranches.Insert(0, "main");
        }

        return validBranches;
    }

    public async Task<List<ModpackRelease>> GetHistoricReleases()
    {
        var client = await GetAuthenticatedClient();

        var releases = await client.Repository.Release.GetAll(RepoOrg, "modpack");
        return releases.Select(
                           x => new ModpackRelease { Version = x.Name.Split(" ")[^1], Timestamp = x.CreatedAt.DateTime, Changelog = FormatChangelog(x.Body) }
                       )
                       .ToList();
    }

    private static string CombineCommitMessages(IReadOnlyCollection<GitHubCommit> commits)
    {
        var filteredCommitMessages = commits.Select(x => x.Commit.Message)
                                            .Reverse()
                                            .Where(x => !x.Contains("Merge branch") && !Regex.IsMatch(x, "Release \\d*\\.\\d*\\.\\d*"))
                                            .ToList();
        return filteredCommitMessages.Count == 0 ? commits.First().Commit.Message : filteredCommitMessages.Aggregate((a, b) => $"{a}\n\n{b}");
    }

    private async Task<Milestone> GetOpenMilestone(string version)
    {
        var client = await GetAuthenticatedClient();
        var milestones = await client.Issue.Milestone.GetAllForRepository(RepoOrg, RepoName, new MilestoneRequest { State = ItemStateFilter.Open });
        var milestone = milestones.FirstOrDefault(x => x.Title == version);
        if (milestone == null)
        {
            _logger.LogWarning($"Could not find open milestone for version {version}");
        }

        return milestone;
    }

    private static void AddChangelogSection(ref string changelog, IReadOnlyCollection<Issue> issues, string header)
    {
        if (issues.Any())
        {
            changelog += $"#### {header}";
            changelog += issues.Select(x => $"\n- {x.Title} [(#{x.Number})]({x.HtmlUrl})").Aggregate((a, b) => a + b);
            changelog += "\n\n";
        }
    }

    private static void AddChangelogUpdated(ref string changelog, IReadOnlyCollection<Issue> issues, string header)
    {
        if (issues.Any())
        {
            changelog += $"#### {header}";
            changelog += issues.Select(
                                   x =>
                                   {
                                       var titleParts = x.Title.Split(" ");
                                       return $"\n- {titleParts[0]} to [{titleParts[1]}]({x.HtmlUrl})";
                                   }
                               )
                               .Aggregate((a, b) => a + b);
            changelog += "\n\n";
        }
    }

    private static string FormatChangelog(string body)
    {
        var changelog = body.Replace("\r\n", "\n").Replace("\n[Report", "<br>[Report");
        var lines = changelog.Split("\n");
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            if (line.StartsWith("  ") && !Regex.Match(line, @"( {2,})-").Success)
            {
                lines[i] = $"<br>{line}";
            }

            var match = Regex.Match(line, @"]\(#\d+\)");
            if (match.Success)
            {
                var number = match.Value.Replace("]", "").Replace("(", "").Replace(")", "").Replace("#", "");
                lines[i] = line.Replace(match.Value, $"](https://github.com/uksf/modpack/issues/{number})");
            }
            else
            {
                match = Regex.Match(line, @"\(#\d+\)");
                if (match.Success)
                {
                    var number = match.Value.Replace("(", "").Replace(")", "").Replace("#", "");
                    lines[i] = line.Replace(match.Value, $"[{match.Value}](https://github.com/uksf/modpack/issues/{number})");
                }
            }
        }

        return string.Join("\n", lines);
    }

    private async Task<GitHubClient> GetAuthenticatedClient()
    {
        GitHubClient client = new(new ProductHeaderValue(AppName)) { Credentials = new(GetJwtToken(), AuthenticationType.Bearer) };
        var response = await client.GitHubApps.CreateInstallationToken(AppInstallation);
        GitHubClient installationClient = new(new ProductHeaderValue(AppName)) { Credentials = new(response.Token) };
        return installationClient;
    }

    private string GetJwtToken()
    {
        GitHubJwtFactory generator = new(new StringPrivateKeySource(_appPrivateKey), new() { AppIntegrationId = AppId, ExpirationSeconds = 540 });
        return generator.CreateEncodedJwtToken();
    }
}
