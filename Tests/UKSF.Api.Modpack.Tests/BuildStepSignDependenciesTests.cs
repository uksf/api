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

public class BuildStepSignDependenciesTests : IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Mock<IProcessCommandFactory> _mockProcessCommandFactory = new();
    private readonly Mock<IBuildProcessTracker> _mockProcessTracker = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly Mock<IVariablesService> _mockVariablesService = new();
    private readonly string _tempDir;

    public BuildStepSignDependenciesTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"uksf_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _mockVariablesService.Setup(x => x.GetVariable("BUILD_FORCE_LOGS")).Returns(new DomainVariableItem { Key = "BUILD_FORCE_LOGS", Item = false });
        _mockVariablesService.Setup(x => x.GetVariable("BUILD_STATE_UPDATE_INTERVAL"))
                             .Returns(new DomainVariableItem { Key = "BUILD_STATE_UPDATE_INTERVAL", Item = 1.0 });
        _mockVariablesService.Setup(x => x.GetVariable("BUILD_SIGNATURES_BATCH_SIZE"))
                             .Returns(new DomainVariableItem { Key = "BUILD_SIGNATURES_BATCH_SIZE", Item = 10 });
        _mockVariablesService.Setup(x => x.GetVariable("BUILD_PATH_DSSIGN")).Returns(new DomainVariableItem { Key = "BUILD_PATH_DSSIGN", Item = "C:\\fake" });
        _mockVariablesService.Setup(x => x.GetVariable("MODPACK_PATH_DEV")).Returns(new DomainVariableItem { Key = "MODPACK_PATH_DEV", Item = _tempDir });
        _mockVariablesService.Setup(x => x.GetVariable("MODPACK_PATH_RC")).Returns(new DomainVariableItem { Key = "MODPACK_PATH_RC", Item = _tempDir });

        _mockProcessCommandFactory.Setup(x => x.CreateCommand(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                                  .Returns((string executable, string workingDir, string args) =>
                                               new ProcessCommand(_mockLogger.Object, executable, workingDir, args)
                                  );
    }

    public void Dispose()
    {
        _cancellationTokenSource.Dispose();
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Theory]
    [InlineData(GameEnvironment.Development, 1, "1.0.0", "uksf_dependencies_dev")]
    [InlineData(GameEnvironment.Development, 50, "1.0.0", "uksf_dependencies_dev")]
    [InlineData(GameEnvironment.Rc, 1, "5.23.7", "uksf_dependencies_5.23.7")]
    [InlineData(GameEnvironment.Rc, 3, "5.23.7", "uksf_dependencies_5.23.7")]
    public void GetKeyname_Should_ReturnCorrectName(GameEnvironment environment, int buildNumber, string version, string expectedKeyName)
    {
        var step = CreateStep(environment, buildNumber, version);
        var keyName = step.TestGetKeyname();
        keyName.Should().Be(expectedKeyName);
    }

    [Fact]
    public void GetKeyname_Should_ThrowForReleaseEnvironment()
    {
        var step = CreateStep(GameEnvironment.Release, 1, "1.0.0");
        var act = () => step.TestGetKeyname();
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CheckGuards_Should_ReturnTrue_FullMode_When_NoBisignsExist()
    {
        SetupAddonsDirectory("mod_a.pbo", "mod_b.pbo");
        var step = CreateStep(GameEnvironment.Development, 1, "1.0.0");

        step.CheckGuards().Should().BeTrue();
        step.TestFullSign.Should().BeTrue();
    }

    [Fact]
    public void CheckGuards_Should_ReturnTrue_PartialMode_When_PboMissingBisign_AndKeyExists()
    {
        var addonsPath = SetupAddonsDirectory("mod_a.pbo", "mod_b.pbo");
        var now = DateTime.UtcNow;
        File.SetLastWriteTimeUtc(Path.Combine(addonsPath, "mod_a.pbo"), now.AddMinutes(-5));
        File.SetLastWriteTimeUtc(Path.Combine(addonsPath, "mod_b.pbo"), now.AddMinutes(-5));
        CreateBisign(addonsPath, "mod_a.pbo", now);
        // mod_b.pbo has no bisign
        CreatePrivateKey();

        var step = CreateStep(GameEnvironment.Development, 1, "1.0.0");
        step.CheckGuards().Should().BeTrue();
        step.TestFullSign.Should().BeFalse();
    }

    [Fact]
    public void CheckGuards_Should_ReturnTrue_FullMode_When_PboMissingBisign_AndNoKeyExists()
    {
        var addonsPath = SetupAddonsDirectory("mod_a.pbo", "mod_b.pbo");
        var now = DateTime.UtcNow;
        File.SetLastWriteTimeUtc(Path.Combine(addonsPath, "mod_a.pbo"), now.AddMinutes(-5));
        File.SetLastWriteTimeUtc(Path.Combine(addonsPath, "mod_b.pbo"), now.AddMinutes(-5));
        CreateBisign(addonsPath, "mod_a.pbo", now);
        // mod_b.pbo has no bisign, no private key exists

        var step = CreateStep(GameEnvironment.Development, 1, "1.0.0");
        step.CheckGuards().Should().BeTrue();
        step.TestFullSign.Should().BeTrue();
    }

    [Fact]
    public void CheckGuards_Should_ReturnTrue_FullMode_When_PboIsNewerThanBisign()
    {
        var addonsPath = SetupAddonsDirectory("mod_a.pbo");
        CreateBisign(addonsPath, "mod_a.pbo", DateTime.UtcNow.AddHours(-1));
        File.SetLastWriteTimeUtc(Path.Combine(addonsPath, "mod_a.pbo"), DateTime.UtcNow);
        CreatePrivateKey();

        var step = CreateStep(GameEnvironment.Development, 1, "1.0.0");
        step.CheckGuards().Should().BeTrue();
        step.TestFullSign.Should().BeTrue();
    }

    [Fact]
    public void CheckGuards_Should_ReturnTrue_FullMode_When_ChangedAndMissingBisigns()
    {
        var addonsPath = SetupAddonsDirectory("mod_a.pbo", "mod_b.pbo");
        var now = DateTime.UtcNow;

        // mod_a is newer than its bisign (changed)
        CreateBisign(addonsPath, "mod_a.pbo", now.AddHours(-1));
        File.SetLastWriteTimeUtc(Path.Combine(addonsPath, "mod_a.pbo"), now);
        // mod_b has no bisign (missing)
        File.SetLastWriteTimeUtc(Path.Combine(addonsPath, "mod_b.pbo"), now.AddMinutes(-5));
        CreatePrivateKey();

        var step = CreateStep(GameEnvironment.Development, 1, "1.0.0");
        step.CheckGuards().Should().BeTrue();
        step.TestFullSign.Should().BeTrue();
    }

    [Fact]
    public void CheckGuards_Should_ReturnFalse_When_AllPbosHaveUpToDateBisigns()
    {
        var addonsPath = SetupAddonsDirectory("mod_a.pbo", "mod_b.pbo");
        var now = DateTime.UtcNow;

        File.SetLastWriteTimeUtc(Path.Combine(addonsPath, "mod_a.pbo"), now.AddMinutes(-5));
        File.SetLastWriteTimeUtc(Path.Combine(addonsPath, "mod_b.pbo"), now.AddMinutes(-5));
        CreateBisign(addonsPath, "mod_a.pbo", now);
        CreateBisign(addonsPath, "mod_b.pbo", now);

        var step = CreateStep(GameEnvironment.Development, 1, "1.0.0");
        step.CheckGuards().Should().BeFalse();
    }

    [Fact]
    public void CheckGuards_Should_ReturnFalse_When_NoPbosExist()
    {
        SetupAddonsDirectory();
        var step = CreateStep(GameEnvironment.Development, 1, "1.0.0");
        step.CheckGuards().Should().BeFalse();
    }

    [Fact]
    public void CheckGuards_Should_IgnoreStaleBisignsForDeletedPbos()
    {
        var addonsPath = SetupAddonsDirectory("mod_a.pbo");
        var now = DateTime.UtcNow;

        File.SetLastWriteTimeUtc(Path.Combine(addonsPath, "mod_a.pbo"), now.AddMinutes(-5));
        CreateBisign(addonsPath, "mod_a.pbo", now);
        File.WriteAllBytes(Path.Combine(addonsPath, "mod_deleted.pbo.uksf_dependencies_dev.bisign"), [0]);

        var step = CreateStep(GameEnvironment.Development, 1, "1.0.0");
        step.CheckGuards().Should().BeFalse();
    }

    [Fact]
    public void CheckGuards_Should_ReturnTrue_FullMode_When_RcBuildNumber1_EvenIfAllSigned()
    {
        var addonsPath = SetupAddonsDirectory("mod_a.pbo");
        var now = DateTime.UtcNow;

        File.SetLastWriteTimeUtc(Path.Combine(addonsPath, "mod_a.pbo"), now.AddMinutes(-5));
        CreateBisign(addonsPath, "mod_a.pbo", now);

        var step = CreateStep(GameEnvironment.Rc, 1, "5.23.7");
        step.CheckGuards().Should().BeTrue();
        step.TestFullSign.Should().BeTrue();
    }

    [Fact]
    public void CheckGuards_Should_ReturnFalse_When_RcBuildNumber2_AndAllSigned()
    {
        var addonsPath = SetupAddonsDirectory("mod_a.pbo");
        var now = DateTime.UtcNow;

        File.SetLastWriteTimeUtc(Path.Combine(addonsPath, "mod_a.pbo"), now.AddMinutes(-5));
        CreateBisign(addonsPath, "mod_a.pbo", now);

        var step = CreateStep(GameEnvironment.Rc, 2, "5.23.7");
        step.CheckGuards().Should().BeFalse();
    }

    [Fact]
    public void CheckGuards_Should_ReturnTrue_PartialMode_When_PboHasMultipleBisigns_AndKeyExists()
    {
        var addonsPath = SetupAddonsDirectory("mod_a.pbo");
        var now = DateTime.UtcNow;

        File.SetLastWriteTimeUtc(Path.Combine(addonsPath, "mod_a.pbo"), now.AddMinutes(-5));
        CreateBisign(addonsPath, "mod_a.pbo", now);
        File.WriteAllBytes(Path.Combine(addonsPath, "mod_a.pbo.uksf_dependencies_old.bisign"), [0]);
        File.SetLastWriteTimeUtc(Path.Combine(addonsPath, "mod_a.pbo.uksf_dependencies_old.bisign"), now);
        CreatePrivateKey();

        var step = CreateStep(GameEnvironment.Development, 1, "1.0.0");
        step.CheckGuards().Should().BeTrue();
        step.TestFullSign.Should().BeFalse();
    }

    [Fact]
    public void CheckGuards_Should_ReturnTrue_FullMode_When_AddonsDirectoryDoesNotExist()
    {
        var step = CreateStep(GameEnvironment.Development, 1, "1.0.0");
        step.CheckGuards().Should().BeTrue();
        step.TestFullSign.Should().BeTrue();
    }

    private string SetupAddonsDirectory(params string[] pboNames)
    {
        var addonsPath = Path.Combine(_tempDir, "Repo", "@uksf_dependencies", "addons");
        Directory.CreateDirectory(addonsPath);
        foreach (var pbo in pboNames)
        {
            File.WriteAllBytes(Path.Combine(addonsPath, pbo), [0]);
        }

        return addonsPath;
    }

    private void CreatePrivateKey()
    {
        var keygenPath = Path.Combine(_tempDir, "PrivateKeys");
        Directory.CreateDirectory(keygenPath);
        File.WriteAllBytes(Path.Combine(keygenPath, "uksf_dependencies_dev.biprivatekey"), [0]);
    }

    private static void CreateBisign(string addonsPath, string pboName, DateTime writeTime)
    {
        var bisignPath = Path.Combine(addonsPath, $"{pboName}.uksf_dependencies_dev.bisign");
        File.WriteAllBytes(bisignPath, [0]);
        File.SetLastWriteTimeUtc(bisignPath, writeTime);
    }

    private TestBuildStepSignDependencies CreateStep(GameEnvironment environment, int buildNumber, string version)
    {
        var step = new TestBuildStepSignDependencies();
        var serviceProvider = new ServiceCollection().AddSingleton(_mockVariablesService.Object)
                                                     .AddSingleton(_mockProcessCommandFactory.Object)
                                                     .AddSingleton(_mockProcessTracker.Object)
                                                     .BuildServiceProvider();

        var build = new DomainModpackBuild
        {
            Id = "test-build",
            Environment = environment,
            BuildNumber = buildNumber,
            Version = version,
            EnvironmentVariables = new Dictionary<string, object>()
        };

        var buildStep = new ModpackBuildStep("Signatures") { Logs = [] };

        step.Init(serviceProvider, _mockLogger.Object, build, buildStep, _ => Task.CompletedTask, () => Task.CompletedTask, _cancellationTokenSource);

        return step;
    }
}

internal class TestBuildStepSignDependencies : BuildStepSignDependencies
{
    public string TestGetKeyname() => GetKeyname();
    public bool TestFullSign => FullSign;
}
