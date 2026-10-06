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

public class BuildStepPatchDependenciesTests : IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly List<(string Executable, string Args)> _commands = [];
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly Mock<IProcessCommandFactory> _mockProcessCommandFactory = new();
    private readonly Mock<IBuildProcessTracker> _mockProcessTracker = new();
    private readonly Mock<IVariablesService> _mockVariablesService = new();
    private readonly string _envDir;
    private readonly string _patcherDir;
    private readonly string _sourcesDir;
    private int _exitCode;

    public BuildStepPatchDependenciesTests()
    {
        var root = Path.Combine(Path.GetTempPath(), $"uksf_test_{Guid.NewGuid():N}");
        _envDir = Path.Combine(root, "env");
        _sourcesDir = Path.Combine(root, "sources");
        _patcherDir = Path.Combine(root, "patcher");
        Directory.CreateDirectory(Path.Combine(_envDir, "Repo", "@uksf_dependencies", "addons"));
        Directory.CreateDirectory(_sourcesDir);

        SetVariable("BUILD_FORCE_LOGS", false);
        SetVariable("BUILD_STATE_UPDATE_INTERVAL", 1.0);
        SetVariable("MODPACK_PATH_DEV", _envDir);
        SetVariable("MODPACK_PATH_RC", _envDir);
        SetVariable("BUILD_PATH_SOURCES", _sourcesDir);
        SetVariable("BUILD_PATH_PATCHER", _patcherDir);

        // Records the requested command and runs a harmless one with the configured exit code instead.
        _mockProcessCommandFactory.Setup(x => x.CreateCommand(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                                  .Returns((string executable, string workingDir, string args) =>
                                      {
                                          _commands.Add((executable, args));
                                          return new ProcessCommand(_mockLogger.Object, "cmd.exe", workingDir, $"/c exit {_exitCode}");
                                      }
                                  );
    }

    private string Patches => Path.Combine(_sourcesDir, "modpack", "patches");
    private string State => Path.Combine(_envDir, "PatcherState");
    private string Addons => Path.Combine(_envDir, "Repo", "@uksf_dependencies", "addons");

    public void Dispose()
    {
        _cancellationTokenSource.Dispose();
        Directory.Delete(Path.GetDirectoryName(_envDir)!, true);
    }

    [Fact]
    public void CheckGuards_Should_ReturnFalse_When_NoManifestsAndNoState()
    {
        CreateStep().CheckGuards().Should().BeFalse();
    }

    [Fact]
    public void CheckGuards_Should_ReturnTrue_When_ManifestsExist()
    {
        Directory.CreateDirectory(Patches);
        File.WriteAllText(Path.Combine(Patches, "a.pbo.json"), "{}");

        CreateStep().CheckGuards().Should().BeTrue();
    }

    [Fact]
    public void CheckGuards_Should_ReturnTrue_When_OnlyStateExists_So_RemovedPatchesAreRestored()
    {
        Directory.CreateDirectory(State);
        File.WriteAllText(Path.Combine(State, "a.pbo.state.json"), "{}");

        CreateStep().CheckGuards().Should().BeTrue();
    }

    [Fact]
    public async Task Setup_Should_Fail_When_PatcherIsMissing()
    {
        var act = () => CreateStep().Setup();

        await act.Should().ThrowAsync<Exception>().WithMessage("*uksf-patcher not found*BUILD_PATH_PATCHER*");
    }

    [Fact]
    public async Task Process_Should_ApplyThenVerify_With_StateOutsideTheRepo()
    {
        CreatePatcher();
        var step = CreateStep();

        await step.Setup();
        await step.Process();

        var expectedArgs = $"--addons \"{Addons}\" --patches \"{Patches}\" --state \"{State}\"";
        _commands.Should()
                 .Equal(
                     (Path.Join(_patcherDir, "uksf-patcher.exe"), $"apply {expectedArgs}"),
                     (Path.Join(_patcherDir, "uksf-patcher.exe"), $"verify {expectedArgs}")
                 );
        State.Should().NotStartWith(Path.Combine(_envDir, "Repo"));
    }

    [Fact]
    public async Task Process_Should_Fail_When_ThePatcherFails()
    {
        CreatePatcher();
        _exitCode = 1;
        var step = CreateStep();
        await step.Setup();

        var act = () => step.Process();

        await act.Should().ThrowAsync<Exception>();
        _commands.Should().ContainSingle(x => x.Args.StartsWith("apply"));
    }

    [Fact]
    public void InvalidateSignatures_Should_RemoveSignaturesOfChangedPbos_Even_When_DatedAfterThePatch()
    {
        var patched = Path.Combine(Addons, "a.pbo");
        var untouched = Path.Combine(Addons, "b.pbo");
        File.WriteAllBytes(patched, [1]);
        File.WriteAllBytes(untouched, [1]);
        var future = DateTime.UtcNow.AddDays(1);
        foreach (var pbo in new[] { patched, untouched })
        {
            File.WriteAllBytes($"{pbo}.uksf_dependencies_dev.bisign", [0]);
            File.SetLastWriteTimeUtc($"{pbo}.uksf_dependencies_dev.bisign", future);
        }

        var step = CreateStep();
        var before = step.SnapshotPbos();
        File.WriteAllBytes(patched, [1, 2]);
        step.InvalidateSignatures(before);

        File.Exists($"{patched}.uksf_dependencies_dev.bisign").Should().BeFalse();
        File.Exists($"{untouched}.uksf_dependencies_dev.bisign").Should().BeTrue();
    }

    private void CreatePatcher()
    {
        Directory.CreateDirectory(_patcherDir);
        File.WriteAllBytes(Path.Join(_patcherDir, "uksf-patcher.exe"), [0]);
    }

    private void SetVariable(string key, object value)
    {
        _mockVariablesService.Setup(x => x.GetVariable(key)).Returns(new DomainVariableItem { Key = key, Item = value });
    }

    private BuildStepPatchDependencies CreateStep()
    {
        var step = new BuildStepPatchDependencies();
        var serviceProvider = new ServiceCollection().AddSingleton(_mockVariablesService.Object)
                                                     .AddSingleton(_mockProcessCommandFactory.Object)
                                                     .AddSingleton(_mockProcessTracker.Object)
                                                     .BuildServiceProvider();
        var build = new DomainModpackBuild
        {
            Id = "test-build",
            Environment = GameEnvironment.Development,
            BuildNumber = 1,
            Version = "1.0.0",
            EnvironmentVariables = new Dictionary<string, object>()
        };
        var buildStep = new ModpackBuildStep(BuildStepPatchDependencies.Name) { Logs = [] };
        step.Init(serviceProvider, _mockLogger.Object, build, buildStep, _ => Task.CompletedTask, () => Task.CompletedTask, _cancellationTokenSource);
        return step;
    }
}
