using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;
using UKSF.Api.Modpack.Services.BuildProcess;
using Xunit;

namespace UKSF.Api.Modpack.Tests;

public class BuildsServiceTests
{
    private readonly Mock<IAccountContext> _mockAccountContext;
    private readonly Mock<IBuildsContext> _mockBuildsContext;
    private readonly Mock<IBuildStepService> _mockBuildStepService;
    private readonly BuildsService _subject;

    public BuildsServiceTests()
    {
        _mockBuildsContext = new();
        _mockBuildStepService = new();
        _mockAccountContext = new();
        Mock<IHttpContextService> mockHttpContextService = new();
        Mock<IUksfLogger> mockLogger = new();

        _subject = new(_mockBuildsContext.Object, _mockBuildStepService.Object, _mockAccountContext.Object, mockHttpContextService.Object, mockLogger.Object);
    }

    [Fact]
    public async Task When_creating_first_rc_build()
    {
        const string Version = "1.1.0";
        _mockBuildsContext.Setup(x => x.Get(It.IsAny<Func<ModpackBuild, bool>>())).Returns(new List<ModpackBuild>());
        _mockAccountContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainAccount, bool>>())).Returns(new DomainAccount { Id = "accountId" });
        _mockBuildStepService.Setup(x => x.GetSteps(GameEnvironment.RC)).Returns(new List<ModpackBuildStep>());

        GithubCommit githubCommit = new() { Author = "author" };
        var result = await _subject.CreateRcBuild(Version, githubCommit);

        result.Environment.Should().Be(GameEnvironment.RC);
        result.BuildNumber.Should().Be(1);
        result.BuilderId.Should().Be("accountId");
        result.EnvironmentVariables.Should().Contain("configuration", "release");
        result.EnvironmentVariables.Should().NotContainKeys("ace_updated", "acre_updated", "uksf_air_updated");
    }

    [Fact]
    public async Task When_creating_rc_build_with_previous_failed_build()
    {
        const string Version = "1.1.0";
        _mockBuildsContext.Setup(x => x.Get(It.IsAny<Func<ModpackBuild, bool>>()))
        .Returns(
            new List<ModpackBuild>
            {
                new()
                {
                    Version = Version,
                    BuildNumber = 1,
                    EnvironmentVariables = new()
                    {
                        { "configuration", "release" },
                        { "ace_updated", true },
                        { "acre_updated", true },
                        { "uksf_air_updated", true }
                    },
                    Steps = new()
                    {
                        new("Build ACE")
                        {
                            Finished = true,
                            BuildResult = ModpackBuildResult.FAILED
                        },
                        new("Build ACRE")
                        {
                            Finished = true,
                            BuildResult = ModpackBuildResult.CANCELLED
                        },
                        new("Build Air") { Finished = false }
                    }
                }
            }
        );
        _mockAccountContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainAccount, bool>>())).Returns(new DomainAccount { Id = "accountId" });
        _mockBuildStepService.Setup(x => x.GetSteps(GameEnvironment.RC)).Returns(new List<ModpackBuildStep>());

        GithubCommit githubCommit = new() { Author = "author" };
        var result = await _subject.CreateRcBuild(Version, githubCommit);

        result.Environment.Should().Be(GameEnvironment.RC);
        result.BuildNumber.Should().Be(2);
        result.BuilderId.Should().Be("accountId");
        result.EnvironmentVariables.Should().Contain("ace_updated", true);
        result.EnvironmentVariables.Should().Contain("ace_updated", true);
        result.EnvironmentVariables.Should().Contain("ace_updated", true);
    }

    [Fact]
    public async Task When_creating_rc_build_with_previous_successful_build()
    {
        const string Version = "1.1.0";
        _mockBuildsContext.Setup(x => x.Get(It.IsAny<Func<ModpackBuild, bool>>()))
        .Returns(
            new List<ModpackBuild>
            {
                new()
                {
                    Version = Version,
                    BuildNumber = 1,
                    EnvironmentVariables = new()
                    {
                        { "configuration", "release" },
                        { "ace_updated", true },
                        { "acre_updated", true },
                        { "uksf_air_updated", true }
                    }
                }
            }
        );
        _mockAccountContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainAccount, bool>>())).Returns(new DomainAccount { Id = "accountId" });
        _mockBuildStepService.Setup(x => x.GetSteps(GameEnvironment.RC)).Returns(new List<ModpackBuildStep>());

        GithubCommit githubCommit = new() { Author = "author" };
        var result = await _subject.CreateRcBuild(Version, githubCommit);

        result.Environment.Should().Be(GameEnvironment.RC);
        result.BuildNumber.Should().Be(2);
        result.BuilderId.Should().Be("accountId");
        result.EnvironmentVariables.Should().NotContainKeys("ace_updated", "acre_updated", "uksf_air_updated");
    }
}
