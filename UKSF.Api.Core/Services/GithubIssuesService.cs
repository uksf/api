using System.Text;
using Octokit;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Request;

namespace UKSF.Api.Core.Services;

public interface IGithubIssuesService
{
    Task<List<IssueTemplate>> GetIssueTemplates();
    Task<Issue> CreateIssue(NewIssueRequest issueRequest);
}

public class GithubIssuesService(IGithubClientService githubClientService, IDisplayNameService displayNameService, IHttpContextService httpContextService)
    : IGithubIssuesService
{
    private readonly SemaphoreSlim _templatesSemaphore = new(1, 1);
    private readonly List<IssueTemplate> _issueTemplates = [];
    private string RepoOrg => githubClientService.RepoOrg;
    private string RepoName => githubClientService.RepoName;

    public async Task<List<IssueTemplate>> GetIssueTemplates()
    {
        await _templatesSemaphore.WaitAsync();
        try
        {
            if (_issueTemplates.Count > 0)
            {
                return _issueTemplates;
            }

            var client = await githubClientService.GetAuthenticatedClient();

            var issueTemplateContents = await client.Repository.Content.GetAllContents(RepoOrg, RepoName, ".github/ISSUE_TEMPLATE");
            foreach (var issueTemplateContent in issueTemplateContents)
            {
                var fileData = await client.Repository.Content.GetRawContent(RepoOrg, RepoName, issueTemplateContent.Path);
                var file = Encoding.UTF8.GetString(fileData);
                var fileParts = file.Split("---");
                var issueConfigurationLines = fileParts[1].Split("\n");

                var nameLine = issueConfigurationLines.FirstOrDefault(x => x.Contains("name:"));
                var aboutLine = issueConfigurationLines.FirstOrDefault(x => x.Contains("about:"));
                var titleLine = issueConfigurationLines.FirstOrDefault(x => x.Contains("title:"));
                var labelsLine = issueConfigurationLines.FirstOrDefault(x => x.Contains("labels:"));
                if (nameLine == null || aboutLine == null || titleLine == null || labelsLine == null)
                {
                    continue;
                }

                var issueName = nameLine.Split(":")[1].Trim();
                var issueDescription = aboutLine.Split(":")[1].Trim();
                var issueTitle = titleLine.Split(":")[1].Replace("\"", "").Trim();
                var issueLabels = labelsLine.Split(":")[1].Replace(" ", "").Split(",").ToList();
                var issueBody = fileParts[2].Trim();

                _issueTemplates.Add(new IssueTemplate(issueName, issueDescription, issueTitle, issueLabels, issueBody));
            }

            return _issueTemplates;
        }
        finally
        {
            _templatesSemaphore.Release();
        }
    }

    public async Task<Issue> CreateIssue(NewIssueRequest issueRequest)
    {
        var client = await githubClientService.GetAuthenticatedClient();

        var newIssue = new NewIssue(issueRequest.Title);
        issueRequest.Labels.ForEach(newIssue.Labels.Add);
        newIssue.Body = issueRequest.Body;

        var user = displayNameService.GetDisplayName(httpContextService.GetUserId());
        newIssue.Body += $"\n\n---\n_**Submitted by:** {user}_";

        var issue = await client.Issue.Create(RepoOrg, RepoName, newIssue);
        return issue;
    }
}
