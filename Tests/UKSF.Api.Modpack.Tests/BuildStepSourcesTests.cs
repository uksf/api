using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Processes;
using UKSF.Api.Core.Services;
using UKSF.Api.Modpack.BuildProcess;
using UKSF.Api.Modpack.BuildProcess.Steps.BuildSteps;
using UKSF.Api.Modpack.Models;
using Xunit;

namespace UKSF.Api.Modpack.Tests;

public class BuildStepSourcesTests : IDisposable
{
    private static readonly string[] StaticMods = ["ace", "acre", "uksf_air"];
    private static readonly string[] RepoMods = ["@uksf_ace", "@uksf_acre2", "@uksf_air"];
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Mock<IGitService> _mockGitService = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly Mock<IVariablesService> _mockVariablesService = new();
    private readonly string _root;

    public BuildStepSourcesTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"uksf test {Guid.NewGuid():N}");
        foreach (var mod in StaticMods.Append("modpack"))
        {
            Directory.CreateDirectory(Path.Combine(_root, "sources", mod));
        }

        foreach (var repo in RepoMods)
        {
            Directory.CreateDirectory(Path.Combine(_root, "env", "Repo", repo));
        }

        SetVariable("BUILD_FORCE_LOGS", false);
        SetVariable("BUILD_STATE_UPDATE_INTERVAL", 1.0);
        SetVariable("BUILD_PATH_SOURCES", Path.Combine(_root, "sources"));
        SetVariable("MODPACK_PATH_DEV", Path.Combine(_root, "env"));

        _mockGitService.Setup(x => x.CreateGitCommand()).Returns(() => new GitCommand(_mockGitService.Object));
        _mockGitService.Setup(x => x.ExecuteCommand(It.IsAny<GitCommandArgs>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync("0123456789abcdef");
    }

    public void Dispose()
    {
        _cancellationTokenSource.Dispose();
        Directory.Delete(_root, true);
    }

    [Fact]
    public async Task Process_Should_MarkUnchangedModsNotUpdated_When_HemttReleaseExists()
    {
        foreach (var mod in StaticMods)
        {
            Directory.CreateDirectory(Path.Combine(_root, "sources", mod, ".hemttout", "release"));
        }

        var build = await RunStep();

        build.EnvironmentVariables.Should().Contain("ace_updated", false).And.Contain("acre_updated", false).And.Contain("uksf_air_updated", false);
    }

    [Fact]
    public async Task Process_Should_MarkModUpdated_When_OnlyALegacyReleaseFolderExists()
    {
        Directory.CreateDirectory(Path.Combine(_root, "sources", "ace", ".hemttout", "release"));
        Directory.CreateDirectory(Path.Combine(_root, "sources", "uksf_air", ".hemttout", "release"));
        Directory.CreateDirectory(Path.Combine(_root, "sources", "acre", "release", "@acre2"));

        var build = await RunStep();

        build.EnvironmentVariables.Should().Contain("acre_updated", true).And.Contain("ace_updated", false);
    }

    private void SetVariable(string key, object value)
    {
        _mockVariablesService.Setup(x => x.GetVariable(key)).Returns(new DomainVariableItem { Key = key, Item = value });
    }

    private async Task<DomainModpackBuild> RunStep()
    {
        var step = new BuildStepSources();
        var serviceProvider = new ServiceCollection().AddSingleton(_mockVariablesService.Object)
                                                     .AddSingleton(_mockGitService.Object)
                                                     .AddSingleton(new Mock<IProcessCommandFactory>().Object)
                                                     .AddSingleton(new Mock<IBuildProcessTracker>().Object)
                                                     .BuildServiceProvider();
        var build = new DomainModpackBuild
        {
            Id = "test-build",
            Environment = GameEnvironment.Development,
            BuildNumber = 1,
            Commit = new GithubCommit { Branch = "refs/heads/main", After = "0123456789abcdef" }
        };

        step.Init(
            serviceProvider,
            _mockLogger.Object,
            build,
            new ModpackBuildStep(BuildStepSources.Name) { Logs = [] },
            _ => Task.CompletedTask,
            () => Task.CompletedTask,
            _cancellationTokenSource
        );
        await step.Setup();
        await step.Process();
        return build;
    }
}
