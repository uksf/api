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

public class BuildStepBuildAcreProcessTests : IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly List<(string Executable, string WorkingDir, string Args)> _commands = [];
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly Mock<IProcessCommandFactory> _mockProcessCommandFactory = new();
    private readonly Mock<IBuildProcessTracker> _mockProcessTracker = new();
    private readonly Mock<IVariablesService> _mockVariablesService = new();
    private readonly string _root;
    private int _exitCode;

    public BuildStepBuildAcreProcessTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"uksf test {Guid.NewGuid():N}");
        Directory.CreateDirectory(Extensions);

        SetVariable("BUILD_FORCE_LOGS", false);
        SetVariable("BUILD_STATE_UPDATE_INTERVAL", 1.0);
        SetVariable("BUILD_PATH_SOURCES", Path.Combine(_root, "sources"));
        SetVariable("MODPACK_PATH_DEV", Path.Combine(_root, "env"));

        _mockProcessCommandFactory.Setup(x => x.CreateCommand(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                                  .Returns((string executable, string workingDir, string args) =>
                                      {
                                          _commands.Add((executable, workingDir, args));
                                          if (executable == "hemtt")
                                          {
                                              var addons = Path.Join(workingDir, ".hemttout", "release", "addons");
                                              Directory.CreateDirectory(addons);
                                              File.WriteAllText(Path.Join(addons, "acre_main.pbo"), "pbo");
                                          }

                                          return new ProcessCommand(_mockLogger.Object, "cmd.exe", workingDir, $"/c exit {_exitCode}");
                                      }
                                  );
    }

    private string AcreRoot => Path.Combine(_root, "sources", "acre");
    private string Extensions => Path.Combine(AcreRoot, "extensions");

    public void Dispose()
    {
        _cancellationTokenSource.Dispose();
        Directory.Delete(_root, true);
    }

    [Fact]
    public async Task Process_Should_BuildExtensionsThenRunHemttAndCopyRelease()
    {
        var step = CreateStep(true);
        await step.Setup();
        await step.Process();

        var expected = AcreExtensionCommands.For(OperatingSystem.IsWindows())
                                            .Select(command => (command.Executable, Extensions, command.Arguments))
                                            .Append(("hemtt", AcreRoot, "release --no-archive -v"));
        _commands.Should().Equal(expected);
        File.Exists(Path.Combine(_root, "env", "Build", "@uksf_acre2", "addons", "acre_main.pbo")).Should().BeTrue();
    }

    [Fact]
    public async Task Process_Should_Fail_When_AnExtensionBuildExitsNonZero()
    {
        var step = CreateStep(true);
        await step.Setup();
        _exitCode = 1;

        var act = () => step.Process();

        await act.Should().ThrowAsync<Exception>().WithMessage("*exit code 1*");
        _commands.Should().ContainSingle();
    }

    [Fact]
    public async Task Process_Should_OnlyCopyRelease_When_AcreDidNotChange()
    {
        var addons = Path.Join(AcreRoot, ".hemttout", "release", "addons");
        Directory.CreateDirectory(addons);
        File.WriteAllText(Path.Join(addons, "acre_main.pbo"), "pbo");
        var step = CreateStep(false);
        await step.Setup();
        await step.Process();

        _commands.Should().BeEmpty();
        File.Exists(Path.Combine(_root, "env", "Build", "@uksf_acre2", "addons", "acre_main.pbo")).Should().BeTrue();
    }

    private void SetVariable(string key, object value)
    {
        _mockVariablesService.Setup(x => x.GetVariable(key)).Returns(new DomainVariableItem { Key = key, Item = value });
    }

    private BuildStepBuildAcre CreateStep(bool acreUpdated)
    {
        var step = new BuildStepBuildAcre();
        var serviceProvider = new ServiceCollection().AddSingleton(_mockVariablesService.Object)
                                                     .AddSingleton(_mockProcessCommandFactory.Object)
                                                     .AddSingleton(_mockProcessTracker.Object)
                                                     .BuildServiceProvider();
        var build = new DomainModpackBuild
        {
            Id = "test-build",
            Environment = GameEnvironment.Development,
            BuildNumber = 1,
            EnvironmentVariables = new Dictionary<string, object> { ["acre_updated"] = acreUpdated }
        };

        step.Init(
            serviceProvider,
            _mockLogger.Object,
            build,
            new ModpackBuildStep(BuildStepBuildAcre.Name) { Logs = [] },
            _ => Task.CompletedTask,
            () => Task.CompletedTask,
            _cancellationTokenSource
        );
        return step;
    }
}
