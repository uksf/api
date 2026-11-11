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

public class BuildStepSignDependenciesProcessTests : IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly List<(string Executable, string WorkingDir, string Args)> _commands = [];
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly Mock<IProcessCommandFactory> _mockProcessCommandFactory = new();
    private readonly Mock<IBuildProcessTracker> _mockProcessTracker = new();
    private readonly Mock<IVariablesService> _mockVariablesService = new();
    private readonly string _root;
    private readonly string _envDir;
    private readonly string _toolingDir;
    private int _exitCode;

    public BuildStepSignDependenciesProcessTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"uksf test {Guid.NewGuid():N}");
        _envDir = Path.Combine(_root, "dev env");
        _toolingDir = Path.Combine(_root, "tooling");
        Directory.CreateDirectory(Addons);
        File.WriteAllBytes(Path.Combine(Addons, "a.pbo"), [0]);

        SetVariable("BUILD_FORCE_LOGS", false);
        SetVariable("BUILD_STATE_UPDATE_INTERVAL", 1.0);
        SetVariable("BUILD_SIGNATURES_BATCH_SIZE", 10);
        SetVariable("MODPACK_PATH_DEV", _envDir);
        SetVariable(UksfTooling.PathVariable, _toolingDir);

        _mockProcessCommandFactory.Setup(x => x.CreateCommand(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                                  .Returns((string executable, string workingDir, string args) =>
                                      {
                                          _commands.Add((executable, workingDir, args));
                                          if (args.StartsWith("create-key"))
                                          {
                                              File.WriteAllBytes(Path.Combine(workingDir, "uksf_dependencies_dev.bikey"), [1]);
                                          }

                                          return ExitProcess.Command(_mockLogger.Object, workingDir, _exitCode);
                                      }
                                  );
    }

    private string Addons => Path.Combine(_envDir, "Repo", "@uksf_dependencies", "addons");
    private string Keys => Path.Combine(_envDir, "Repo", "@uksf_dependencies", "keys");
    private string PrivateKeys => Path.Combine(_envDir, "PrivateKeys");
    private string Signer => Path.Combine(_toolingDir, UksfTooling.FileName("uksf-sign"));

    public void Dispose()
    {
        _cancellationTokenSource.Dispose();
        Directory.Delete(_root, true);
    }

    [Fact]
    public async Task FullSign_Should_CreateKeyAndSignEachPbo_With_UksfSign()
    {
        CreateSigner();
        var step = CreateStep();
        step.CheckGuards().Should().BeTrue();

        await step.Setup();
        await step.Process();

        var privateKey = Path.Combine(PrivateKeys, "uksf_dependencies_dev.biprivatekey");
        _commands.Should()
                 .Equal(
                     (Signer, PrivateKeys, "create-key \"uksf_dependencies_dev\""),
                     (Signer, Addons, $"sign \"{privateKey}\" \"{Path.Combine(Addons, "a.pbo")}\"")
                 );
        File.Exists(Path.Combine(Keys, "uksf_dependencies_dev.bikey")).Should().BeTrue();
    }

    [Fact]
    public async Task Setup_Should_FailBeforeTouchingKeys_When_UksfSignIsMissing()
    {
        Directory.CreateDirectory(Keys);
        var existingKey = Path.Combine(Keys, "old.bikey");
        File.WriteAllBytes(existingKey, [2]);
        var step = CreateStep();
        step.CheckGuards().Should().BeTrue();

        var act = () => step.Setup();

        await act.Should().ThrowAsync<Exception>().WithMessage($"*uksf-sign not found*{UksfTooling.PathVariable}*");
        _commands.Should().BeEmpty();
        File.Exists(existingKey).Should().BeTrue();
    }

    [Fact]
    public async Task Process_Should_Fail_When_UksfSignExitsNonZero()
    {
        CreateSigner();
        var step = CreateStep();
        step.CheckGuards().Should().BeTrue();
        await step.Setup();
        _exitCode = 1;

        var act = () => step.Process();

        await act.Should().ThrowAsync<Exception>().WithMessage("*Failed to sign file*a.pbo*");
    }

    private void CreateSigner()
    {
        Directory.CreateDirectory(_toolingDir);
        File.WriteAllBytes(Signer, [0]);
    }

    private void SetVariable(string key, object value)
    {
        _mockVariablesService.Setup(x => x.GetVariable(key)).Returns(new DomainVariableItem { Key = key, Item = value });
    }

    private BuildStepSignDependencies CreateStep()
    {
        var step = new BuildStepSignDependencies();
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

        step.Init(
            serviceProvider,
            _mockLogger.Object,
            build,
            new ModpackBuildStep(BuildStepSignDependencies.Name) { Logs = [] },
            _ => Task.CompletedTask,
            () => Task.CompletedTask,
            _cancellationTokenSource
        );
        return step;
    }
}
