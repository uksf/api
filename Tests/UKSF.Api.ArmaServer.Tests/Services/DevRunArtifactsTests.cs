using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using UKSF.Api.ArmaServer.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class DevRunArtifactsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "devrun-artifacts-" + Guid.NewGuid());
    private readonly string _profiles;
    private readonly string _configs;
    private readonly string _missions;

    public DevRunArtifactsTests()
    {
        _profiles = Directory.CreateDirectory(Path.Combine(_root, "profiles")).FullName;
        _configs = Directory.CreateDirectory(Path.Combine(_root, "configs")).FullName;
        _missions = Directory.CreateDirectory(Path.Combine(_root, "missions")).FullName;
    }

    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public void CleanUp_deletes_dev_run_missions_and_configs_and_keeps_newest_profile_runs_with_their_headless_clients()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var run = 0; run < DevRunArtifacts.RetainedProfileRuns + 2; run++)
        {
            var name = $"DevRun_{run:x8}";
            var profile = Directory.CreateDirectory(Path.Combine(_profiles, name));
            File.WriteAllText(Path.Combine(profile.FullName, "arma3server_x64.rpt"), "rpt");
            profile.CreationTimeUtc = start.AddMinutes(run);
            Directory.CreateDirectory(Path.Combine(_profiles, name + "hc1")).CreationTimeUtc = start.AddMinutes(run);
            Directory.CreateDirectory(Path.Combine(_missions, name + ".VR", "functions"));
            File.WriteAllText(Path.Combine(_configs, name + ".cfg"), "cfg");
        }

        Directory.CreateDirectory(Path.Combine(_profiles, "Main"));
        Directory.CreateDirectory(Path.Combine(_missions, "FLOPS_Sumava.brf_sumava"));
        File.WriteAllText(Path.Combine(_configs, "Main.cfg"), "cfg");

        DevRunArtifacts.CleanUp(_profiles, _configs, _missions).Should().BeEmpty();

        Directory.GetDirectories(_missions).Select(Path.GetFileName).Should().Equal("FLOPS_Sumava.brf_sumava");
        Directory.GetFiles(_configs).Select(Path.GetFileName).Should().Equal("Main.cfg");
        var profiles = Directory.GetDirectories(_profiles).Select(Path.GetFileName).ToList();
        profiles.Should().HaveCount(DevRunArtifacts.RetainedProfileRuns * 2 + 1);
        profiles.Should().Contain(["Main", "DevRun_00000015", "DevRun_00000015hc1", "DevRun_00000002", "DevRun_00000002hc1"]);
        profiles.Should().NotContain(["DevRun_00000000", "DevRun_00000000hc1", "DevRun_00000001", "DevRun_00000001hc1"]);
    }

    [Fact]
    public void CleanUp_reports_a_locked_config_and_still_deletes_the_rest()
    {
        var locked = Path.Combine(_configs, "DevRun_locked01.cfg");
        File.WriteAllText(locked, "cfg");
        Directory.CreateDirectory(Path.Combine(_missions, "DevRun_free0001.VR"));

        using (File.Open(locked, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            DevRunArtifacts.CleanUp(_profiles, _configs, _missions).Should().ContainSingle().Which.Should().StartWith(locked);
        }

        Directory.GetDirectories(_missions).Should().BeEmpty();
    }

    [Fact]
    public void CleanUp_ignores_missing_roots()
    {
        var act = () => DevRunArtifacts.CleanUp(Path.Combine(_root, "absent"), null, "");

        act.Should().NotThrow();
    }
}
