using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Modpack.BuildProcess;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;
using Xunit;

namespace UKSF.Api.Modpack.Tests;

public class BuildsServiceTests
{
    private readonly Mock<IAccountContext> _mockAccountContext;
    private readonly Mock<IBuildsContext> _mockBuildsContext;
    private readonly Mock<IBuildStepService> _mockBuildStepService;
    private readonly Mock<IBuildProcessTracker> _mockProcessTracker;
    private readonly BuildsService _subject;

    public BuildsServiceTests()
    {
        _mockBuildsContext = new Mock<IBuildsContext>();
        _mockBuildStepService = new Mock<IBuildStepService>();
        _mockAccountContext = new Mock<IAccountContext>();
        _mockProcessTracker = new Mock<IBuildProcessTracker>();
        Mock<IHttpContextService> mockHttpContextService = new();
        Mock<IUksfLogger> mockLogger = new();

        _subject = new BuildsService(
            _mockBuildsContext.Object,
            _mockBuildStepService.Object,
            _mockAccountContext.Object,
            mockHttpContextService.Object,
            _mockProcessTracker.Object,
            mockLogger.Object
        );
    }

    [Fact]
    public async Task EmergencyCleanupStuckBuilds_Should_MarkBuildsAsCancelled()
    {
        // Arrange
        _mockProcessTracker.Setup(x => x.KillTrackedProcesses(null)).Returns(2);

        var runningBuild = new DomainModpackBuild
        {
            Id = "running-build",
            Running = true,
            Steps = new List<ModpackBuildStep> { new("Test Step") { Running = true } }
        };

        _mockBuildsContext.Setup(x => x.Get(It.IsAny<Func<DomainModpackBuild, bool>>())).Returns(new List<DomainModpackBuild> { runningBuild });

        // Act
        await _subject.EmergencyCleanupStuckBuilds();

        // Assert
        runningBuild.Running.Should().BeFalse();
        runningBuild.Finished.Should().BeTrue();
        runningBuild.BuildResult.Should().Be(ModpackBuildResult.Cancelled);

        var runningStep = runningBuild.Steps.First();
        runningStep.Running.Should().BeFalse();
        runningStep.Finished.Should().BeTrue();
        runningStep.BuildResult.Should().Be(ModpackBuildResult.Cancelled);
    }

    [Fact]
    public async Task EmergencyCleanupStuckBuilds_Should_ReturnZero_WhenNoRunningBuilds()
    {
        // Arrange
        _mockProcessTracker.Setup(x => x.KillTrackedProcesses(null)).Returns(0);
        _mockBuildsContext.Setup(x => x.Get(It.IsAny<Func<DomainModpackBuild, bool>>())).Returns(new List<DomainModpackBuild>());

        // Act
        var killedCount = await _subject.EmergencyCleanupStuckBuilds();

        // Assert
        killedCount.Should().Be(0);
        _mockProcessTracker.Verify(x => x.KillTrackedProcesses(null), Times.Once);
    }

    [Fact]
    public async Task EmergencyCleanupStuckBuilds_Should_UseProcessTracker_ToKillStuckProcesses()
    {
        // Arrange
        _mockProcessTracker.Setup(x => x.KillTrackedProcesses(null)).Returns(3);

        var runningBuild = new DomainModpackBuild
        {
            Id = "running-build",
            Running = true,
            Steps = new List<ModpackBuildStep> { new("Test Step") { Running = true } }
        };

        _mockBuildsContext.Setup(x => x.Get(It.IsAny<Func<DomainModpackBuild, bool>>())).Returns(new List<DomainModpackBuild> { runningBuild });

        // Act
        var killedCount = await _subject.EmergencyCleanupStuckBuilds();

        // Assert
        killedCount.Should().Be(3);
        _mockProcessTracker.Verify(x => x.KillTrackedProcesses(null), Times.Once);
    }

    [Fact]
    public async Task When_creating_first_rc_build()
    {
        const string Version = "1.1.0";
        _mockBuildsContext.Setup(x => x.Get(It.IsAny<Func<DomainModpackBuild, bool>>())).Returns(new List<DomainModpackBuild>());
        _mockAccountContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainAccount, bool>>())).Returns(new DomainAccount { Id = "accountId" });
        _mockBuildStepService.Setup(x => x.GetSteps(GameEnvironment.Rc)).Returns([]);

        GithubCommit githubCommit = new() { Author = "author" };
        var result = await _subject.CreateRcBuild(Version, githubCommit);

        result.Environment.Should().Be(GameEnvironment.Rc);
        result.BuildNumber.Should().Be(1);
        result.BuilderId.Should().Be("accountId");
        result.EnvironmentVariables.Should().Contain("configuration", "release");
        result.EnvironmentVariables.Should().NotContainKeys("ace_updated", "acre_updated", "uksf_air_updated");
    }

    [Fact]
    public async Task When_creating_rc_build_with_previous_failed_build()
    {
        const string Version = "1.1.0";
        _mockBuildsContext.Setup(x => x.Get(It.IsAny<Func<DomainModpackBuild, bool>>()))
                          .Returns(
                              new List<DomainModpackBuild>
                              {
                                  new()
                                  {
                                      Version = Version,
                                      BuildNumber = 1,
                                      EnvironmentVariables = new Dictionary<string, object>
                                      {
                                          { "configuration", "release" },
                                          { "ace_updated", true },
                                          { "acre_updated", true },
                                          { "uksf_air_updated", true }
                                      },
                                      Steps =
                                      [
                                          new ModpackBuildStep("Build ACE") { Finished = true, BuildResult = ModpackBuildResult.Failed },

                                          new ModpackBuildStep("Build ACRE") { Finished = true, BuildResult = ModpackBuildResult.Cancelled },

                                          new ModpackBuildStep("Build Air") { Finished = false }
                                      ]
                                  }
                              }
                          );

        _mockAccountContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainAccount, bool>>())).Returns(new DomainAccount { Id = "accountId" });
        _mockBuildStepService.Setup(x => x.GetSteps(GameEnvironment.Rc)).Returns([]);

        GithubCommit githubCommit = new() { Author = "author" };
        var result = await _subject.CreateRcBuild(Version, githubCommit);

        result.Environment.Should().Be(GameEnvironment.Rc);
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
        _mockBuildsContext.Setup(x => x.Get(It.IsAny<Func<DomainModpackBuild, bool>>()))
                          .Returns(
                              new List<DomainModpackBuild>
                              {
                                  new()
                                  {
                                      Version = Version,
                                      BuildNumber = 1,
                                      EnvironmentVariables = new Dictionary<string, object>
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
        _mockBuildStepService.Setup(x => x.GetSteps(GameEnvironment.Rc)).Returns([]);

        GithubCommit githubCommit = new() { Author = "author" };
        var result = await _subject.CreateRcBuild(Version, githubCommit);

        result.Environment.Should().Be(GameEnvironment.Rc);
        result.BuildNumber.Should().Be(2);
        result.BuilderId.Should().Be("accountId");
        result.EnvironmentVariables.Should().NotContainKeys("ace_updated", "acre_updated", "uksf_air_updated");
    }
}
