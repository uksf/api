using GitHubJwt;
using Microsoft.Extensions.Options;
using Octokit;
using UKSF.Api.Core.Configuration;

namespace UKSF.Api.Core.Services;

public interface IGithubClientService
{
    public string RepoOrg => "uksf";
    public string RepoName => "modpack";
    Task<GitHubClient> GetAuthenticatedClient();
}

public class GithubClientService : IGithubClientService
{
    private const int AppId = 53456;
    private const long AppInstallation = 6681715;
    private const string AppName = "uksf-api-integration";
    private readonly string _appPrivateKey;

    public GithubClientService(IOptions<AppSettings> options)
    {
        var appSettings = options.Value;
        _appPrivateKey = appSettings.Secrets.Github.AppPrivateKey;
    }

    public async Task<GitHubClient> GetAuthenticatedClient()
    {
        var client = new GitHubClient(new ProductHeaderValue(AppName)) { Credentials = new Credentials(GetJwtToken(), AuthenticationType.Bearer) };
        var response = await client.GitHubApps.CreateInstallationToken(AppInstallation);
        return new GitHubClient(new ProductHeaderValue(AppName)) { Credentials = new Credentials(response.Token) };
    }

    private string GetJwtToken()
    {
        var generator = new GitHubJwtFactory(
            new StringPrivateKeySource(_appPrivateKey),
            new GitHubJwtFactoryOptions { AppIntegrationId = AppId, ExpirationSeconds = 540 }
        );
        return generator.CreateEncodedJwtToken();
    }
}
