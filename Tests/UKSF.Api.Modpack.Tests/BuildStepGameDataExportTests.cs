using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Processes;
using UKSF.Api.Core.Services;
using UKSF.Api.Modpack.BuildProcess;
using UKSF.Api.Modpack.BuildProcess.Steps.ReleaseSteps;
using UKSF.Api.Modpack.Models;
using Xunit;

namespace UKSF.Api.Modpack.Tests;

public class BuildStepGameDataExportTests
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Mock<IGameDataExportService> _mockGameDataExportService = new();
    private readonly Mock<IProcessCommandFactory> _mockProcessCommandFactory = new();
    private readonly Mock<IBuildProcessTracker> _mockProcessTracker = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly Mock<IVariablesService> _mockVariablesService = new();
    private readonly DomainModpackBuild _build;

    public BuildStepGameDataExportTests()
    {
        _mockVariablesService.Setup(x => x.GetVariable("BUILD_STATE_UPDATE_INTERVAL"))
                             .Returns(new DomainVariableItem { Key = "BUILD_STATE_UPDATE_INTERVAL", Item = 1.0 });

        _build = new DomainModpackBuild
        {
            Id = "test-build",
            Version = "5.23.9",
            Environment = GameEnvironment.Release,
            EnvironmentVariables = new Dictionary<string, object>()
        };
    }

    [Fact]
    public async Task ProcessExecute_CallsTriggerWithBuildVersion()
    {
        _mockGameDataExportService.Setup(x => x.Trigger(It.IsAny<string>())).Returns(new TriggerResult(TriggerOutcome.Started, "id-1"));
        var step = CreateStep();

        await step.Setup();
        await step.Process();

        _mockGameDataExportService.Verify(x => x.Trigger(_build.Version), Times.Once);
    }

    [Fact]
    public async Task ProcessExecute_DoesNotThrow_WhenAlreadyRunning()
    {
        _mockGameDataExportService.Setup(x => x.Trigger(It.IsAny<string>())).Returns(new TriggerResult(TriggerOutcome.AlreadyRunning, "in-flight"));
        var step = CreateStep();

        await step.Setup();
        Func<Task> act = async () => await step.Process();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ProcessExecute_DoesNotThrow_WhenTriggerThrows()
    {
        _mockGameDataExportService.Setup(x => x.Trigger(It.IsAny<string>())).Throws(new InvalidOperationException("simulated launcher failure"));
        var step = CreateStep();

        await step.Setup();
        Func<Task> act = async () => await step.Process();

        await act.Should().NotThrowAsync();
    }

    private BuildStepGameDataExport CreateStep()
    {
        var step = new BuildStepGameDataExport();
        var serviceProvider = new ServiceCollection().AddSingleton(_mockVariablesService.Object)
                                                     .AddSingleton(_mockProcessCommandFactory.Object)
                                                     .AddSingleton(_mockProcessTracker.Object)
                                                     .AddSingleton(_mockGameDataExportService.Object)
                                                     .BuildServiceProvider();

        var buildStep = new ModpackBuildStep(BuildStepGameDataExport.Name) { Logs = [] };

        step.Init(serviceProvider, _mockLogger.Object, _build, buildStep, _ => Task.CompletedTask, () => Task.CompletedTask, _cancellationTokenSource);

        return step;
    }
}
