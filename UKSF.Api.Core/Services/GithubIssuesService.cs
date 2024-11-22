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

public class GithubIssuesService(
    IGithubClientService githubClientService,
    IDisplayNameService displayNameService,
    IHttpContextService httpContextService
) : IGithubIssuesService
{
    private readonly List<IssueTemplate> _issueTemplates = [];
    private string RepoOrg => githubClientService.RepoOrg;
    private string RepoName => githubClientService.RepoName;

    public async Task<List<IssueTemplate>> GetIssueTemplates()
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

            var issueName = issueConfigurationLines.First(x => x.Contains("name:")).Split(":")[1].Trim();
            var issueDescription = issueConfigurationLines.First(x => x.Contains("about:")).Split(":")[1].Trim();
            var issueTitle = issueConfigurationLines.First(x => x.Contains("title:")).Split(":")[1].Replace("\"", "").Trim();
            var issueLabels = issueConfigurationLines.First(x => x.Contains("labels:")).Split(":")[1].Replace(" ", "").Split(",").ToList();
            var issueBody = fileParts[2].Trim();

            _issueTemplates.Add(new IssueTemplate(issueName, issueDescription, issueTitle, issueLabels, issueBody));
        }

        return _issueTemplates;
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
