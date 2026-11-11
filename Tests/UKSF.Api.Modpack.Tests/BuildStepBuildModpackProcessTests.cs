using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Processes;
using UKSF.Api.Core.Services;
using UKSF.Api.Modpack.BuildProcess;
using UKSF.Api.Modpack.BuildProcess.Steps.BuildSteps.Mods;
using UKSF.Api.Modpack.Models;
using Xunit;

namespace UKSF.Api.Modpack.Tests;

public class BuildStepBuildModpackProcessTests : IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly List<(string Executable, string WorkingDir, string Args)> _commands = [];
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly Mock<IProcessCommandFactory> _mockProcessCommandFactory = new();
    private readonly Mock<IBuildProcessTracker> _mockProcessTracker = new();
    private readonly Mock<IVariablesService> _mockVariablesService = new();
    private readonly string _root;
    private int _exitCode;

    public BuildStepBuildModpackProcessTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"uksf test {Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(ModpackRoot, "addons", "main"));
        Directory.CreateDirectory(Extension);

        SetVariable("BUILD_FORCE_LOGS", false);
        SetVariable("BUILD_STATE_UPDATE_INTERVAL", 1.0);
        SetVariable("BUILD_PATH_PYTHON", "python");
        SetVariable("BUILD_PATH_SOURCES", Path.Combine(_root, "sources"));
        SetVariable("MODPACK_PATH_DEV", Path.Combine(_root, "env"));

        _mockProcessCommandFactory.Setup(x => x.CreateCommand(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                                  .Returns((string executable, string workingDir, string args) =>
                                      {
                                          _commands.Add((executable, workingDir, args));
                                          FakeOutputs(executable, workingDir, args);
                                          return new ProcessCommand(_mockLogger.Object, "cmd.exe", workingDir, $"/c exit {_exitCode}");
                                      }
                                  );
    }

    private string ModpackRoot => Path.Combine(_root, "sources", "modpack");
    private string Extension => Path.Combine(ModpackRoot, "extension");

    public void Dispose()
    {
        _cancellationTokenSource.Dispose();
        Directory.Delete(_root, true);
    }

    [Fact]
    public async Task Process_Should_BuildBothExtensionsFetchWhisperAndRunHemtt()
    {
        var step = CreateStep();
        await step.Setup();
        await step.Process();

        var whisperCache = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "uksf", "whisper");
        var expected = ExtensionTargets.For(OperatingSystem.IsWindows())
                                       .Select(target => (target.Executable, Extension, target.Arguments))
                                       .Append(
                                           ("pwsh", ModpackRoot,
                                            $"-NoProfile -ExecutionPolicy Bypass -File {Path.Join("tools", "fetch_whisper_model.ps1")} -CacheDir \"{whisperCache}\"")
                                       )
                                       .Append(("hemtt", ModpackRoot, "release --no-archive -v"));
        _commands.Should().Equal(expected);
        File.ReadAllText(Path.Combine(ModpackRoot, "uksf_x64.dll")).Should().Be("uksf_x64.dll");
        File.ReadAllText(Path.Combine(ModpackRoot, "uksf_x64.so")).Should().Be("uksf_x64.so");
        File.Exists(Path.Combine(_root, "env", "Build", "@uksf", "addons", "uksf_main.pbo")).Should().BeTrue();
    }

    [Fact]
    public async Task Process_Should_Fail_When_AnExtensionBuildExitsNonZero()
    {
        var step = CreateStep();
        await step.Setup();
        _exitCode = 1;

        var act = () => step.Process();

        await act.Should().ThrowAsync<Exception>().WithMessage("*exit code 1*");
        _commands.Should().ContainSingle();
    }

    private void FakeOutputs(string executable, string workingDir, string args)
    {
        var target = ExtensionTargets.For(OperatingSystem.IsWindows()).FirstOrDefault(x => x.Executable == executable && x.Arguments == args);
        if (target is not null)
        {
            var built = Path.Join(workingDir, target.BuiltPath);
            Directory.CreateDirectory(Path.GetDirectoryName(built)!);
            File.WriteAllText(built, target.FileName);
        }

        if (executable == "hemtt")
        {
            var addons = Path.Join(workingDir, ".hemttout", "release", "addons");
            Directory.CreateDirectory(addons);
            File.WriteAllText(Path.Join(addons, "uksf_main.pbo"), "pbo");
        }
    }

    private void SetVariable(string key, object value)
    {
        _mockVariablesService.Setup(x => x.GetVariable(key)).Returns(new DomainVariableItem { Key = key, Item = value });
    }

    private BuildStepBuildModpack CreateStep()
    {
        var step = new BuildStepBuildModpack();
        var serviceProvider = new ServiceCollection().AddSingleton(_mockVariablesService.Object)
                                                     .AddSingleton(_mockProcessCommandFactory.Object)
                                                     .AddSingleton(_mockProcessTracker.Object)
                                                     .BuildServiceProvider();
        var build = new DomainModpackBuild
        {
            Id = "test-build",
            Environment = GameEnvironment.Development,
            BuildNumber = 1,
            EnvironmentVariables = new Dictionary<string, object> { ["configuration"] = "development" }
        };

        step.Init(
            serviceProvider,
            _mockLogger.Object,
            build,
            new ModpackBuildStep(BuildStepBuildModpack.Name) { Logs = [] },
            _ => Task.CompletedTask,
            () => Task.CompletedTask,
            _cancellationTokenSource
        );
        return step;
    }
}
