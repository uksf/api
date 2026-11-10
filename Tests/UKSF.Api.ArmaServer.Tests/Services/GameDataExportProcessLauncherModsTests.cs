using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class GameDataExportProcessLauncherModsTests : IDisposable
{
    private readonly Mock<ISyntheticServerLauncher> _mockSyntheticLauncher = new();
    private readonly Mock<IVariablesService> _mockVariablesService = new();
    private readonly List<string> _tempDirectories = [];
    private SyntheticLaunchSpec _captured;

    public GameDataExportProcessLauncherModsTests()
    {
        SetupVariable("SERVER_PATH_RELEASE", "/server/release");
        SetupVariable("MODPACK_PATH_RELEASE", "/modpack/release");
        SetupVariable("SERVER_COMMAND_PASSWORD", "testcmdpass");

        _mockSyntheticLauncher.Setup(x => x.Launch(It.IsAny<SyntheticLaunchSpec>()))
                              .Callback<SyntheticLaunchSpec>(s => _captured = s)
                              .Returns(new SyntheticLaunchResult(4242, "p", "m"));
    }

    public void Dispose()
    {
        foreach (var directory in _tempDirectories)
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void Launch_Mods_FallsBackToRepoPath_WhenNoAtSubdirs()
    {
        var modpackRoot = CreateModpackRoot();

        Launch();

        _captured.Mods.Should().ContainSingle().Which.Should().Be(Path.Combine(modpackRoot, "Repo"));
    }

    [Fact]
    public void Launch_Mods_EnumeratesAtSubdirs_WhenPresent()
    {
        CreateModpackRoot("@CBA_A3", "@uksf", "@uksf_ace", "Backup");

        Launch();

        _captured.Mods.Should().HaveCount(3);
        _captured.Mods.Should().Contain(m => m.EndsWith("@CBA_A3"));
        _captured.Mods.Should().Contain(m => m.EndsWith("@uksf"));
        _captured.Mods.Should().Contain(m => m.EndsWith("@uksf_ace"));
        _captured.Mods.Should().NotContain(m => m.Contains("Backup"));
    }

    [Fact]
    public void Launch_Mods_FallsBackToRepoPath_WhenRepoPathDoesNotExist()
    {
        SetupVariable("MODPACK_PATH_RELEASE", "/nonexistent/path");

        Launch();

        _captured.Mods.Should().ContainSingle().Which.Should().Be(Path.Combine("/nonexistent/path", "Repo"));
    }

    [Fact]
    public void Launch_Mods_AppendsOcap_FromServerPathMods()
    {
        CreateModpackRoot("@uksf");
        var serverMods = CreateTempDirectory();
        Directory.CreateDirectory(Path.Combine(serverMods, "@ocap"));
        SetupVariable("SERVER_PATH_MODS", serverMods);

        Launch();

        _captured.Mods.Should().HaveCount(2);
        _captured.Mods.Should().Contain(m => m.EndsWith("@uksf"));
        _captured.Mods.Should().Contain(Path.Combine(serverMods, "@ocap"));
    }

    [Fact]
    public void Launch_Mods_OmitsOcap_WhenServerPathModsHasNoOcapDir()
    {
        CreateModpackRoot("@uksf");
        SetupVariable("SERVER_PATH_MODS", CreateTempDirectory());

        Launch();

        _captured.Mods.Should().ContainSingle().Which.Should().EndWith("@uksf");
    }

    [Fact]
    public void Launch_Mods_OmitsOcap_WhenServerPathModsVariableUnset()
    {
        CreateModpackRoot("@uksf");

        Launch();

        _captured.Mods.Should().ContainSingle().Which.Should().EndWith("@uksf");
    }

    private void Launch()
    {
        new GameDataExportProcessLauncher(_mockSyntheticLauncher.Object, _mockVariablesService.Object).Launch("5.23.8");
    }

    private string CreateModpackRoot(params string[] repoSubdirectories)
    {
        var modpackRoot = CreateTempDirectory();
        var repoPath = Path.Combine(modpackRoot, "Repo");
        Directory.CreateDirectory(repoPath);
        foreach (var subdirectory in repoSubdirectories)
        {
            Directory.CreateDirectory(Path.Combine(repoPath, subdirectory));
        }

        SetupVariable("MODPACK_PATH_RELEASE", modpackRoot);
        return modpackRoot;
    }

    private string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(path);
        _tempDirectories.Add(path);
        return path;
    }

    private void SetupVariable(string key, string value)
    {
        _mockVariablesService.Setup(x => x.GetVariable(key)).Returns(new DomainVariableItem { Key = key, Item = value });
    }
}
