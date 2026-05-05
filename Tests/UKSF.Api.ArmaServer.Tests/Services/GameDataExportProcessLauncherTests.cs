using System.IO;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class GameDataExportProcessLauncherTests
{
    private readonly Mock<ISyntheticServerLauncher> _mockSyntheticLauncher = new();
    private readonly Mock<IVariablesService> _mockVariablesService = new();

    public GameDataExportProcessLauncherTests()
    {
        SetupVariable("SERVER_PATH_RELEASE", "/server/release");
        SetupVariable("MODPACK_PATH_RELEASE", "/modpack/release");
        SetupVariable("SERVER_COMMAND_PASSWORD", "testcmdpass");

        _mockSyntheticLauncher.Setup(x => x.Launch(It.IsAny<SyntheticLaunchSpec>()))
                              .Returns(new SyntheticLaunchResult(4242, "/profiles/GameDataExport", "/missions/GameDataExport.VR"));
    }

    private static DomainVariableItem CreateVariable(string key, object item) => new() { Key = key, Item = item };

    private void SetupVariable(string key, string value) => _mockVariablesService.Setup(x => x.GetVariable(key)).Returns(CreateVariable(key, value));

    private GameDataExportProcessLauncher CreateSut() => new(_mockSyntheticLauncher.Object, _mockVariablesService.Object);

    [Fact]
    public void Launch_DelegatesToSyntheticLauncher()
    {
        var sut = CreateSut();
        sut.Launch("5.23.8");

        _mockSyntheticLauncher.Verify(x => x.Launch(It.IsAny<SyntheticLaunchSpec>()), Times.Once);
    }

    [Fact]
    public void Launch_BuildsSpecWithCorrectProfileAndMissionName()
    {
        SyntheticLaunchSpec captured = null;
        _mockSyntheticLauncher.Setup(x => x.Launch(It.IsAny<SyntheticLaunchSpec>()))
                              .Callback<SyntheticLaunchSpec>(s => captured = s)
                              .Returns(new SyntheticLaunchResult(4242, "p", "m"));

        CreateSut().Launch("5.23.8");

        captured.Should().NotBeNull();
        captured.ProfileName.Should().Be("GameDataExport");
        captured.MissionName.Should().Be("GameDataExport.VR");
        captured.ConfigFileName.Should().Be("GameDataExport.cfg");
    }

    [Fact]
    public void Launch_BuildsSpecWithCorrectPorts()
    {
        SyntheticLaunchSpec captured = null;
        _mockSyntheticLauncher.Setup(x => x.Launch(It.IsAny<SyntheticLaunchSpec>()))
                              .Callback<SyntheticLaunchSpec>(s => captured = s)
                              .Returns(new SyntheticLaunchResult(4242, "p", "m"));

        CreateSut().Launch("5.23.8");

        captured.GamePort.Should().Be(3302);
        captured.ApiPort.Should().Be(3303);
    }

    [Fact]
    public void Launch_BuildsSpecWithCorrectServerExecutable()
    {
        SyntheticLaunchSpec captured = null;
        _mockSyntheticLauncher.Setup(x => x.Launch(It.IsAny<SyntheticLaunchSpec>()))
                              .Callback<SyntheticLaunchSpec>(s => captured = s)
                              .Returns(new SyntheticLaunchResult(4242, "p", "m"));

        CreateSut().Launch("5.23.8");

        captured.ServerExecutablePath.Should().EndWith("arma3server_x64.exe");
    }

    [Fact]
    public void Launch_BuildsSpecWithDescriptionExtContainingPostInit()
    {
        SyntheticLaunchSpec captured = null;
        _mockSyntheticLauncher.Setup(x => x.Launch(It.IsAny<SyntheticLaunchSpec>()))
                              .Callback<SyntheticLaunchSpec>(s => captured = s)
                              .Returns(new SyntheticLaunchResult(4242, "p", "m"));

        CreateSut().Launch("5.23.8");

        captured.DescriptionExt.Should().Contain("postInit = 1");
        captured.DescriptionExt.Should().Contain("respawn = \"INSTANT\"");
        captured.DescriptionExt.Should().Contain("class CfgFunctions");
    }

    [Fact]
    public void Launch_BuildsSpecWithMissionSqmContainingRequiredEntries()
    {
        SyntheticLaunchSpec captured = null;
        _mockSyntheticLauncher.Setup(x => x.Launch(It.IsAny<SyntheticLaunchSpec>()))
                              .Callback<SyntheticLaunchSpec>(s => captured = s)
                              .Returns(new SyntheticLaunchResult(4242, "p", "m"));

        CreateSut().Launch("5.23.8");

        captured.MissionSqm.Should().Contain("binarizationWanted=0");
        captured.MissionSqm.Should().Contain("isPlayable=1");
        captured.MissionSqm.Should().Contain("B_Soldier_F");
        captured.MissionSqm.Should().Contain("sourceName=\"GameDataExport\"");
        captured.MissionSqm.Should().Contain("\"A3_Characters_F\"");
    }

    [Fact]
    public void Launch_BuildsSpecWithServerConfigContainingRequiredEntries()
    {
        SyntheticLaunchSpec captured = null;
        _mockSyntheticLauncher.Setup(x => x.Launch(It.IsAny<SyntheticLaunchSpec>()))
                              .Callback<SyntheticLaunchSpec>(s => captured = s)
                              .Returns(new SyntheticLaunchResult(4242, "p", "m"));

        CreateSut().Launch("5.23.8");

        captured.ServerConfig.Should().Contain("persistent = 1;");
        captured.ServerConfig.Should().Contain("serverCommandPassword = \"testcmdpass\"");
        captured.ServerConfig.Should().Contain("template = \"GameDataExport.VR\"");
    }

    [Fact]
    public void Launch_BuildsSpecWithRunExportSqfFunctionFile()
    {
        SyntheticLaunchSpec captured = null;
        _mockSyntheticLauncher.Setup(x => x.Launch(It.IsAny<SyntheticLaunchSpec>()))
                              .Callback<SyntheticLaunchSpec>(s => captured = s)
                              .Returns(new SyntheticLaunchResult(4242, "p", "m"));

        CreateSut().Launch("5.23.8");

        captured.FunctionFiles.Should().ContainKey("fn_runExport.sqf");
        captured.FunctionFiles["fn_runExport.sqf"].Should().Contain("uksf_common_fnc_gameDataExport");
        captured.FunctionFiles["fn_runExport.sqf"].Should().Contain("fileExportFinish");
    }

    [Fact]
    public void Launch_ReturnsExpectedOutputDirectoryAndGlobs()
    {
        var result = CreateSut().Launch("5.23.9");

        result.ProcessId.Should().Be(4242);
        result.ExpectedOutputDirectory.Should().EndWith("uksf_exports");
        result.ConfigGlob.Should().Be("config_*_uksf-5-23-9.cpp");
        result.CbaSettingsGlob.Should().Be("cba_settings_*_uksf-5-23-9.sqf");
        result.CbaSettingsReferenceGlob.Should().Be("cba_settings_reference_*_uksf-5-23-9.json");
    }

    [Theory]
    [InlineData("5.23.8", "config_*_uksf-5-23-8.cpp", "cba_settings_*_uksf-5-23-8.sqf", "cba_settings_reference_*_uksf-5-23-8.json")]
    [InlineData("5.23",   "config_*_uksf-5-23.cpp",   "cba_settings_*_uksf-5-23.sqf",   "cba_settings_reference_*_uksf-5-23.json")]
    [InlineData("6.0.1",  "config_*_uksf-6-0-1.cpp",  "cba_settings_*_uksf-6-0-1.sqf",  "cba_settings_reference_*_uksf-6-0-1.json")]
    [InlineData("6",      "config_*_uksf-6.cpp",      "cba_settings_*_uksf-6.sqf",      "cba_settings_reference_*_uksf-6.json")]
    public void Launch_GlobsMatchSqfFilenameContract(string modpackVersion, string expectedConfig, string expectedSettings, string expectedReference)
    {
        var result = CreateSut().Launch(modpackVersion);

        result.ConfigGlob.Should().Be(expectedConfig);
        result.CbaSettingsGlob.Should().Be(expectedSettings);
        result.CbaSettingsReferenceGlob.Should().Be(expectedReference);
    }

    [Fact]
    public void Launch_Mods_FallsBackToRepoPath_WhenNoAtSubdirs()
    {
        var tempModpackRoot = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString());
        var repoPath = Path.Combine(tempModpackRoot, "Repo");
        Directory.CreateDirectory(repoPath);
        SetupVariable("MODPACK_PATH_RELEASE", tempModpackRoot);

        SyntheticLaunchSpec captured = null;
        _mockSyntheticLauncher.Setup(x => x.Launch(It.IsAny<SyntheticLaunchSpec>()))
                              .Callback<SyntheticLaunchSpec>(s => captured = s)
                              .Returns(new SyntheticLaunchResult(4242, "p", "m"));

        try
        {
            CreateSut().Launch("5.23.8");

            captured.Mods.Should().HaveCount(1);
            captured.Mods[0].Should().Contain("Repo");
        }
        finally
        {
            try
            {
                Directory.Delete(tempModpackRoot, recursive: true);
            }
            catch
            {
                /* best-effort */
            }
        }
    }

    [Fact]
    public void Launch_Mods_EnumeratesAtSubdirs_WhenPresent()
    {
        var tempModpackRoot = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString());
        var repoPath = Path.Combine(tempModpackRoot, "Repo");
        Directory.CreateDirectory(Path.Combine(repoPath, "@CBA_A3"));
        Directory.CreateDirectory(Path.Combine(repoPath, "@uksf"));
        Directory.CreateDirectory(Path.Combine(repoPath, "@uksf_ace"));
        Directory.CreateDirectory(Path.Combine(repoPath, "Backup")); // should be excluded
        SetupVariable("MODPACK_PATH_RELEASE", tempModpackRoot);

        SyntheticLaunchSpec captured = null;
        _mockSyntheticLauncher.Setup(x => x.Launch(It.IsAny<SyntheticLaunchSpec>()))
                              .Callback<SyntheticLaunchSpec>(s => captured = s)
                              .Returns(new SyntheticLaunchResult(4242, "p", "m"));

        try
        {
            CreateSut().Launch("5.23.8");

            captured.Mods.Should().HaveCount(3);
            captured.Mods.Should().Contain(m => m.Contains("@CBA_A3"));
            captured.Mods.Should().Contain(m => m.Contains("@uksf"));
            captured.Mods.Should().Contain(m => m.Contains("@uksf_ace"));
            captured.Mods.Should().NotContain(m => m.Contains("Backup"));
        }
        finally
        {
            try
            {
                Directory.Delete(tempModpackRoot, recursive: true);
            }
            catch
            {
                /* best-effort */
            }
        }
    }

    [Fact]
    public void Launch_Mods_FallsBackToRepoPath_WhenRepoPathDoesNotExist()
    {
        SetupVariable("MODPACK_PATH_RELEASE", "/nonexistent/path");

        SyntheticLaunchSpec captured = null;
        _mockSyntheticLauncher.Setup(x => x.Launch(It.IsAny<SyntheticLaunchSpec>()))
                              .Callback<SyntheticLaunchSpec>(s => captured = s)
                              .Returns(new SyntheticLaunchResult(4242, "p", "m"));

        CreateSut().Launch("5.23.8");

        captured.Mods.Should().HaveCount(1);
        captured.Mods[0].Should().Contain("Repo");
    }
}
