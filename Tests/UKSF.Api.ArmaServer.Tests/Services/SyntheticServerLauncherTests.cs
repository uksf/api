using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core.Processes;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class SyntheticServerLauncherTests : IDisposable
{
    private readonly Mock<IProcessUtilities> _mockProcessUtilities = new();
    private readonly string _tempRoot;
    private readonly string _serverExePath;
    private readonly string _profilesRoot;
    private readonly string _configsRoot;
    private readonly string _missionsRoot;
    private readonly string _modPath;

    public SyntheticServerLauncherTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempRoot);
        _serverExePath = Path.Combine(_tempRoot, "arma3server_x64.exe");
        File.WriteAllText(_serverExePath, "stub");
        _profilesRoot = Path.Combine(_tempRoot, "profiles");
        _configsRoot = Path.Combine(_tempRoot, "configs");
        _missionsRoot = Path.Combine(_tempRoot, "missions");
        _modPath = Path.Combine(_tempRoot, "@uksf_main");
        Directory.CreateDirectory(_profilesRoot);
        Directory.CreateDirectory(_configsRoot);
        Directory.CreateDirectory(_missionsRoot);
        Directory.CreateDirectory(_modPath);
        _mockProcessUtilities.Setup(x => x.LaunchManagedProcess(It.IsAny<string>(), It.IsAny<string>())).Returns(7777);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
        catch
        {
            /* best-effort */
        }
    }

    private SyntheticLaunchSpec BuildSpec(IReadOnlyList<string> mods = null) =>
        new(
            ProfileName: "TestSynth",
            ConfigFileName: "TestSynth.cfg",
            MissionName: "TestSynth.VR",
            ServerExecutablePath: _serverExePath,
            GamePort: 3304,
            ApiPort: 3305,
            Mods: mods ?? new[] { _modPath },
            ServerConfig: "hostname=\"x\";",
            MissionSqm: "version=54;",
            DescriptionExt: "respawn=\"INSTANT\";",
            FunctionFiles: new Dictionary<string, string> { ["fn_user.sqf"] = "diag_log \"hi\";" }
        );

    [Fact]
    public void Launch_writes_config_profile_and_mission_files_then_starts_process()
    {
        var launcher = new SyntheticServerLauncher(_mockProcessUtilities.Object, _profilesRoot, _configsRoot, _missionsRoot);

        var result = launcher.Launch(BuildSpec());

        result.ProcessId.Should().Be(7777);
        File.Exists(Path.Combine(_configsRoot, "TestSynth.cfg")).Should().BeTrue();
        Directory.Exists(Path.Combine(_profilesRoot, "TestSynth")).Should().BeTrue();
        var missionDir = Path.Combine(_missionsRoot, "TestSynth.VR");
        File.Exists(Path.Combine(missionDir, "mission.sqm")).Should().BeTrue();
        File.Exists(Path.Combine(missionDir, "description.ext")).Should().BeTrue();
        File.Exists(Path.Combine(missionDir, "functions", "fn_user.sqf")).Should().BeTrue();
    }

    [Fact]
    public void Launch_args_include_server_autoinit_and_mod_list()
    {
        var launcher = new SyntheticServerLauncher(_mockProcessUtilities.Object, _profilesRoot, _configsRoot, _missionsRoot);

        launcher.Launch(BuildSpec());

        _mockProcessUtilities.Verify(x => x.LaunchManagedProcess(
                                         _serverExePath,
                                         It.Is<string>(args => args.Contains("-server") &&
                                                               args.Contains("-autoInit") &&
                                                               args.Contains($"-mod=\"{_modPath}\"") &&
                                                               args.Contains("-port=3304") &&
                                                               args.Contains("-apiport=3305")
                                         )
                                     )
        );
    }

    [Fact]
    public void Launch_throws_InvalidModPathException_with_missing_paths()
    {
        var launcher = new SyntheticServerLauncher(_mockProcessUtilities.Object, _profilesRoot, _configsRoot, _missionsRoot);
        var missing = "C:/nonexistent/@nope";

        var act = () => launcher.Launch(BuildSpec(new[] { _modPath, missing }));

        act.Should().Throw<InvalidModPathException>().Which.MissingPaths.Should().BeEquivalentTo(new[] { missing });
    }

    [Fact]
    public void Launch_joins_multiple_mods_with_semicolons()
    {
        var modB = Path.Combine(Path.GetDirectoryName(_modPath)!, "@cba_a3");
        Directory.CreateDirectory(modB);
        var launcher = new SyntheticServerLauncher(_mockProcessUtilities.Object, _profilesRoot, _configsRoot, _missionsRoot);

        launcher.Launch(BuildSpec(new[] { _modPath, modB }));

        _mockProcessUtilities.Verify(x => x.LaunchManagedProcess(It.IsAny<string>(), It.Is<string>(args => args.Contains($"-mod=\"{_modPath};{modB}\""))));
    }
}
