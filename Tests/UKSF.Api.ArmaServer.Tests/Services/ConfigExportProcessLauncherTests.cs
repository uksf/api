using System;
using System.IO;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Processes;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class ConfigExportProcessLauncherTests : IDisposable
{
    private readonly Mock<IVariablesService> _mockVariablesService = new();
    private readonly Mock<IProcessUtilities> _mockProcessUtilities = new();
    private readonly string _tempCfgDir;
    private readonly string _tempProfilesDir;
    private readonly string _tempServerRoot;
    private readonly string _tempModpackRoot;
    private readonly string _tempMissionsRoot;

    public ConfigExportProcessLauncherTests()
    {
        _tempServerRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _tempCfgDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _tempProfilesDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _tempModpackRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _tempMissionsRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        Directory.CreateDirectory(_tempServerRoot);
        Directory.CreateDirectory(_tempCfgDir);
        Directory.CreateDirectory(_tempProfilesDir);
        Directory.CreateDirectory(_tempModpackRoot);
        Directory.CreateDirectory(_tempMissionsRoot);

        SetupVariable("SERVER_PATH_RELEASE", _tempServerRoot);
        SetupVariable("SERVER_PATH_CONFIGS", _tempCfgDir);
        SetupVariable("SERVER_PATH_PROFILES", _tempProfilesDir);
        SetupVariable("MODPACK_PATH_RELEASE", _tempModpackRoot);
        SetupVariable("MISSIONS_PATH", _tempMissionsRoot);
        SetupVariable("SERVER_COMMAND_PASSWORD", "testcmdpass");

        _mockProcessUtilities.Setup(x => x.LaunchManagedProcess(It.IsAny<string>(), It.IsAny<string>())).Returns(4242);
    }

    public void Dispose()
    {
        TryDelete(_tempServerRoot);
        TryDelete(_tempCfgDir);
        TryDelete(_tempProfilesDir);
        TryDelete(_tempModpackRoot);
        TryDelete(_tempMissionsRoot);
    }

    private static void TryDelete(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
            /* best-effort */
        }
    }

    private static DomainVariableItem CreateVariable(string key, object item)
    {
        return new DomainVariableItem { Key = key, Item = item };
    }

    private void SetupVariable(string key, string value)
    {
        _mockVariablesService.Setup(x => x.GetVariable(key)).Returns(CreateVariable(key, value));
    }

    private ConfigExportProcessLauncher CreateSut()
    {
        return new ConfigExportProcessLauncher(_mockProcessUtilities.Object, _mockVariablesService.Object);
    }

    [Fact]
    public void Launch_WritesConfigFileWithRequiredEntries()
    {
        var sut = CreateSut();
        sut.Launch("5.23.9");

        var cfgPath = Path.Combine(_tempCfgDir, "ConfigExport.cfg");
        File.Exists(cfgPath).Should().BeTrue();
        var content = File.ReadAllText(cfgPath);
        content.Should().Contain("persistent = 1;");
        content.Should().Contain("serverCommandPassword = \"testcmdpass\";");
        content.Should().Contain("template = \"ConfigExport.VR\";");
    }

    [Fact]
    public void Launch_PassesExecutableAutoInitAndCorrectPorts()
    {
        var sut = CreateSut();
        sut.Launch("5.23.9");

        _mockProcessUtilities.Verify(
            x => x.LaunchManagedProcess(
                It.Is<string>(exe => exe.EndsWith("arma3server_x64.exe")),
                It.Is<string>(args => args.Contains("-server") && args.Contains("-autoInit") && args.Contains("-port=3302") && args.Contains("-apiport=3303"))
            ),
            Times.Once
        );
    }

    [Fact]
    public void Launch_ReturnsExpectedOutputDirectoryAndGlob()
    {
        var sut = CreateSut();
        var result = sut.Launch("5.23.9");
        result.ProcessId.Should().Be(4242);
        result.ExpectedOutputDirectory.Should().EndWith("uksf_config_export");
        result.ExpectedFilenameGlob.Should().Be("config_*_uksf-5-23-9.cpp");
    }

    [Fact]
    public void Launch_CreatesProfileDirectory()
    {
        var sut = CreateSut();
        sut.Launch("5.23.9");

        var profilePath = Path.Combine(_tempProfilesDir, "ConfigExport");
        Directory.Exists(profilePath).Should().BeTrue();
    }

    [Fact]
    public void Launch_BuildsModChainFromAtSubdirs()
    {
        var repoPath = Path.Combine(_tempModpackRoot, "Repo");
        Directory.CreateDirectory(Path.Combine(repoPath, "@CBA_A3"));
        Directory.CreateDirectory(Path.Combine(repoPath, "@uksf"));
        Directory.CreateDirectory(Path.Combine(repoPath, "@uksf_ace"));
        Directory.CreateDirectory(Path.Combine(repoPath, "Backup"));

        var sut = CreateSut();
        sut.Launch("5.23.9");

        _mockProcessUtilities.Verify(
            x => x.LaunchManagedProcess(
                It.IsAny<string>(),
                It.Is<string>(args => args.Contains("@CBA_A3") && args.Contains("@uksf") && args.Contains("@uksf_ace") && !args.Contains("Backup"))
            ),
            Times.Once
        );
    }

    [Fact]
    public void Launch_FallsBackToSingleModPath_WhenNoAtSubdirs()
    {
        var repoPath = Path.Combine(_tempModpackRoot, "Repo");
        Directory.CreateDirectory(repoPath);

        var sut = CreateSut();
        sut.Launch("5.23.9");

        _mockProcessUtilities.Verify(
            x => x.LaunchManagedProcess(It.IsAny<string>(), It.Is<string>(args => args.Contains("-mod=\"") && args.Contains("Repo"))),
            Times.Once
        );
    }

    [Fact]
    public void Launch_WritesMissionFiles_WithPlayableUnitAndInstantRespawn()
    {
        var sut = CreateSut();
        sut.Launch("5.23.9");

        var missionDir = Path.Combine(_tempMissionsRoot, "ConfigExport.VR");
        Directory.Exists(missionDir).Should().BeTrue();

        var sqm = File.ReadAllText(Path.Combine(missionDir, "mission.sqm"));
        sqm.Should().Contain("version=54");
        sqm.Should().Contain("isPlayable=1"); // CRITICAL — -autoInit needs a playable slot to simulate the "first client" into
        sqm.Should().Contain("B_Soldier_F");
        sqm.Should().Contain("binarizationWanted=0"); // raw mission needs this to skip .ebo lookup
        sqm.Should().Contain("sourceName=\"ConfigExport\"");
        sqm.Should().Contain("\"A3_Characters_F\""); // explicit dep — without addons[] engine fails with misleading "DLC deleted" warning

        var description = File.ReadAllText(Path.Combine(missionDir, "description.ext"));
        description.Should().Contain("respawn = \"INSTANT\";"); // INSTANT does not need a respawn marker; BASE does
        description.Should().Contain("class Header");
        description.Should().Contain("class CfgFunctions"); // postInit registration is what runs the export non-scheduled
        description.Should().Contain("postInit = 1");

        var runExport = File.ReadAllText(Path.Combine(missionDir, "functions", "fn_runExport.sqf"));
        runExport.Should().Contain("uksf_common_fnc_configExport");
        runExport.Should().Contain("configExportFinish");
    }

    [Fact]
    public void Launch_GlobContainsModpackVersion()
    {
        var sut = CreateSut();
        var result = sut.Launch("6.0.1");
        result.ExpectedFilenameGlob.Should().Be("config_*_uksf-6-0-1.cpp");
    }

    [Theory]
    [InlineData("5.23.8", "config_*_uksf-5-23-8.cpp")]
    [InlineData("5.23", "config_*_uksf-5-23.cpp")]
    [InlineData("6.0.1", "config_*_uksf-6-0-1.cpp")]
    [InlineData("6", "config_*_uksf-6.cpp")]
    public void Launch_GlobMatchesSqfFilenameContract(string modpackVersion, string expectedGlob)
    {
        var sut = CreateSut();
        var result = sut.Launch(modpackVersion);
        result.ExpectedFilenameGlob.Should().Be(expectedGlob);
    }
}
