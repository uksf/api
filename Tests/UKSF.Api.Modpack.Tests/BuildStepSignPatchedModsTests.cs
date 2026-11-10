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

public class BuildStepSignPatchedModsTests : IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Mock<IVariablesService> _mockVariablesService = new();
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"uksf_test_{Guid.NewGuid():N}");

    public BuildStepSignPatchedModsTests()
    {
        foreach (var (key, value) in new (string, object)[]
                 {
                     ("BUILD_FORCE_LOGS", false), ("BUILD_STATE_UPDATE_INTERVAL", 1.0), ("MODPACK_PATH_DEV", _tempDir), ("MODPACK_PATH_RC", _tempDir)
                 })
        {
            _mockVariablesService.Setup(x => x.GetVariable(key)).Returns(new DomainVariableItem { Key = key, Item = value });
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Dispose();
        Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void DependencyPbos_Should_Include_PatchedPbosOfOtherMods_And_Exclude_TheirUpstreamSignedPbos()
    {
        var dependencies = Addons("@uksf_dependencies", "dep.pbo");
        var cup = Addons("@CUP_Vehicles", "patched.pbo", "resigned.pbo", "upstream.pbo");
        Addons("@unpatched_mod", "other.pbo");
        File.WriteAllBytes(Path.Combine(cup, "resigned.pbo.uksf_dependencies_dev.bisign"), [0]);
        File.WriteAllBytes(Path.Combine(cup, "upstream.pbo.cup_vehicles-1.19.2.bisign"), [0]);
        Directory.CreateDirectory(Path.Combine(_tempDir, "PatcherState", "@CUP_Vehicles"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "PatcherState", "@uksf_dependencies"));

        var pbos = CreateStep().DependencyPbos().Select(x => x.FullName);

        pbos.Should().BeEquivalentTo(Path.Combine(dependencies, "dep.pbo"), Path.Combine(cup, "patched.pbo"), Path.Combine(cup, "resigned.pbo"));
    }

    [Fact]
    public void DependencyPbos_Should_Include_PatchedPbo_When_Its_Signatures_Have_Zsync_Files()
    {
        var cup = Addons("@CUP_Vehicles", "cup_wheeledvehicles_tigr.pbo");
        File.WriteAllBytes(Path.Combine(cup, "cup_wheeledvehicles_tigr.pbo.uksf_dependencies_dev.bisign"), [0]);
        File.WriteAllBytes(Path.Combine(cup, "cup_wheeledvehicles_tigr.pbo.uksf_dependencies_dev.bisign.zsync"), [0]);
        File.WriteAllBytes(Path.Combine(cup, "cup_wheeledvehicles_tigr.pbo.cup_vehicles-1.19.2.bisign.zsync"), [0]);
        Directory.CreateDirectory(Path.Combine(_tempDir, "PatcherState", "@CUP_Vehicles"));

        var pbos = CreateStep().DependencyPbos().Select(x => x.FullName);

        pbos.Should().BeEquivalentTo(Path.Combine(cup, "cup_wheeledvehicles_tigr.pbo"));
    }

    [Theory]
    [InlineData(new byte[] { 1, 2, 3, 9 }, true)]
    [InlineData(new byte[] { 7, 7, 7, 9 }, false)]
    public void CheckGuards_Should_PartialSign_Only_When_A_Signature_Was_Made_With_Another_Key(byte[] bisignBytes, bool signedWithCurrentKey)
    {
        Addons("@uksf_dependencies");
        var keys = Path.Combine(_tempDir, "Repo", "@uksf_dependencies", "keys");
        Directory.CreateDirectory(keys);
        File.WriteAllBytes(Path.Combine(keys, "uksf_dependencies_dev.bikey"), [1, 2, 3]);
        Directory.CreateDirectory(Path.Combine(_tempDir, "PrivateKeys"));
        File.WriteAllBytes(Path.Combine(_tempDir, "PrivateKeys", "uksf_dependencies_dev.biprivatekey"), [0]);
        var cup = Addons("@CUP_Vehicles", "cup_wheeledvehicles_tigr.pbo");
        File.SetLastWriteTimeUtc(Path.Combine(cup, "cup_wheeledvehicles_tigr.pbo"), DateTime.UtcNow.AddMinutes(-5));
        File.WriteAllBytes(Path.Combine(cup, "cup_wheeledvehicles_tigr.pbo.uksf_dependencies_dev.bisign"), bisignBytes);
        Directory.CreateDirectory(Path.Combine(_tempDir, "PatcherState", "@CUP_Vehicles"));
        var step = CreateStep();

        step.CheckGuards().Should().Be(!signedWithCurrentKey);
        step.FullSign.Should().BeFalse();
    }

    private string Addons(string mod, params string[] pbos)
    {
        var addons = Path.Combine(_tempDir, "Repo", mod, "addons");
        Directory.CreateDirectory(addons);
        foreach (var pbo in pbos)
        {
            File.WriteAllBytes(Path.Combine(addons, pbo), [0]);
        }

        return addons;
    }

    private BuildStepSignDependencies CreateStep()
    {
        var step = new BuildStepSignDependencies();
        var serviceProvider = new ServiceCollection().AddSingleton(_mockVariablesService.Object)
                                                     .AddSingleton(new Mock<IProcessCommandFactory>().Object)
                                                     .AddSingleton(new Mock<IBuildProcessTracker>().Object)
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
            new Mock<IUksfLogger>().Object,
            build,
            new ModpackBuildStep("Signatures") { Logs = [] },
            _ => Task.CompletedTask,
            () => Task.CompletedTask,
            _cancellationTokenSource
        );
        return step;
    }
}
