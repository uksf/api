using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Core.Services;
using UKSF.Api.Modpack.BuildProcess;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;
using UKSF.Api.Modpack.Signalr.Clients;
using UKSF.Api.Modpack.Signalr.Hubs;
using Xunit;

namespace UKSF.Api.Modpack.Tests.Services;

public class ModpackServiceNewBuildTests
{
    private readonly Mock<IBuildsService> _buildsService = new();
    private readonly Mock<IBuildQueueService> _buildQueueService = new();
    private readonly Mock<IGithubService> _githubService = new();
    private readonly Mock<IHttpContextService> _httpContextService = new();
    private readonly ModpackService _subject;

    public ModpackServiceNewBuildTests()
    {
        _githubService.Setup(x => x.GetLatestReferenceCommit("main"))
        .ReturnsAsync(
            new GithubCommit
            {
                Branch = "main",
                After = "sha",
                Message = "Previous commit message",
                Author = "author@uksf.co.uk"
            }
        );
        _githubService.Setup(x => x.GetReferenceVersion("main")).ReturnsAsync("5.23.7");
        _buildsService.Setup(x => x.CreateDevBuild(It.IsAny<string>(), It.IsAny<GithubCommit>(), It.IsAny<NewBuild>()))
                      .ReturnsAsync((string version, GithubCommit commit, NewBuild _) => new DomainModpackBuild { Version = version, Commit = commit });

        _subject = new ModpackService(
            new Mock<IReleasesContext>().Object,
            new Mock<IBuildsContext>().Object,
            new Mock<IReleaseService>().Object,
            _buildsService.Object,
            _buildQueueService.Object,
            _githubService.Object,
            new Mock<IVersionService>().Object,
            new Mock<IVariablesService>().Object,
            new Mock<IHubContext<ModpackHub, IModpackClient>>().Object,
            _httpContextService.Object,
            new Mock<IGitService>().Object,
            new Mock<IWorkshopModsService>().Object,
            new Mock<IUksfLogger>().Object
        );
    }

    [Fact]
    public async Task NewBuild_WithChanges_ShouldReplaceCommitMessageAndQueueBuild()
    {
        await _subject.NewBuild(new NewBuild { Reference = "main", Changes = "#### Added\n- cTAB Connect" });

        _buildsService.Verify(
            x => x.CreateDevBuild("5.23.7", It.Is<GithubCommit>(commit => commit.Message == "#### Added\n- cTAB Connect"), It.IsAny<NewBuild>()),
            Times.Once
        );
        _buildQueueService.Verify(x => x.QueueBuild(It.IsAny<DomainModpackBuild>()), Times.Once);
    }

    [Fact]
    public async Task NewBuild_WithoutChanges_ShouldKeepCommitMessage()
    {
        await _subject.NewBuild(new NewBuild { Reference = "main" });

        _buildsService.Verify(
            x => x.CreateDevBuild("5.23.7", It.Is<GithubCommit>(commit => commit.Message == "Previous commit message"), It.IsAny<NewBuild>()),
            Times.Once
        );
    }
}
