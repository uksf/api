using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Processes;
using UKSF.Api.Core.Services;
using UKSF.Api.Modpack.BuildProcess;
using UKSF.Api.Modpack.BuildProcess.Steps.Common;
using UKSF.Api.Modpack.Models;
using Xunit;

namespace UKSF.Api.Modpack.Tests;

public class BuildStepBuildRepoTests : IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly List<(string Executable, string WorkingDirectory, string Args)> _commands = [];
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly Mock<IProcessCommandFactory> _mockProcessCommandFactory = new();
    private readonly Mock<IBuildProcessTracker> _mockProcessTracker = new();
    private readonly Mock<IVariablesService> _mockVariablesService = new();
    private readonly string _arma3SyncDir = Path.Combine(Path.GetTempPath(), $"uksf_test_{Guid.NewGuid():N}");

    public BuildStepBuildRepoTests()
    {
        Directory.CreateDirectory(_arma3SyncDir);
        SetVariable("BUILD_FORCE_LOGS", false);
        SetVariable("BUILD_STATE_UPDATE_INTERVAL", 1.0);
        SetVariable("BUILD_PATH_ARMA3SYNC", _arma3SyncDir);

        _mockProcessCommandFactory.Setup(x => x.CreateCommand(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                                  .Returns((string executable, string workingDir, string args) =>
                                      {
                                          _commands.Add((executable, workingDir, args));
                                          return ExitProcess.EchoThenExit(_mockLogger.Object, workingDir, "Repository built", 0);
                                      }
                                  );
    }

    public void Dispose()
    {
        _cancellationTokenSource.Dispose();
        Directory.Delete(_arma3SyncDir, true);
    }

    [Theory]
    [InlineData(GameEnvironment.Development, "UKSF-Dev")]
    [InlineData(GameEnvironment.Rc, "UKSF-Rc")]
    [InlineData(GameEnvironment.Release, "UKSF")]
    public async Task Process_Should_BuildTheEnvironmentRepo_With_OneSha1Worker(GameEnvironment environment, string repoName)
    {
        var step = CreateStep(environment);

        await step.Setup();
        await step.Process();

        _commands.Should().ContainSingle().Which.Should().Be(("java", _arma3SyncDir, $"-XX:ActiveProcessorCount=1 -jar ArmA3Sync.jar -BUILD {repoName}"));
    }

    private void SetVariable(string key, object value)
    {
        _mockVariablesService.Setup(x => x.GetVariable(key)).Returns(new DomainVariableItem { Key = key, Item = value });
    }

    private BuildStepBuildRepo CreateStep(GameEnvironment environment)
    {
        var step = new BuildStepBuildRepo();
        var serviceProvider = new ServiceCollection().AddSingleton(_mockVariablesService.Object)
                                                     .AddSingleton(_mockProcessCommandFactory.Object)
                                                     .AddSingleton(_mockProcessTracker.Object)
                                                     .BuildServiceProvider();
        var build = new DomainModpackBuild
        {
            Id = "test-build",
            Environment = environment,
            BuildNumber = 1,
            Version = "1.0.0",
            EnvironmentVariables = new Dictionary<string, object>()
        };
        var buildStep = new ModpackBuildStep(BuildStepBuildRepo.Name) { Logs = [] };
        step.Init(serviceProvider, _mockLogger.Object, build, buildStep, _ => Task.CompletedTask, () => Task.CompletedTask, _cancellationTokenSource);
        return step;
    }
}
