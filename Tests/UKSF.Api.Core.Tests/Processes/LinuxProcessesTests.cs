using System;
using System.IO;
using FluentAssertions;
using UKSF.Api.Core.Processes;
using Xunit;

namespace UKSF.Api.Core.Tests.Processes;

public class LinuxProcessesTests : IDisposable
{
    private readonly string _procRoot = Path.Combine(Path.GetTempPath(), $"proc-{Guid.NewGuid():N}");
    private static readonly DateTime Now = new(2026, 11, 10, 17, 5, 9, 42);

    public void Dispose()
    {
        if (Directory.Exists(_procRoot))
        {
            Directory.Delete(_procRoot, true);
        }
    }

    [Fact]
    public void LogPath_uses_profiles_directory_and_rpt_name()
    {
        var path = LinuxProcesses.LogPath("/opt/arma/release/arma3server_x64", "-port=2302 -profiles=/srv/profiles/Main -config=x.cfg", Now);

        path.Should().Be(Path.Combine("/srv/profiles/Main", "arma3server_x64_2026-11-10_17-05-09-042.rpt"));
    }

    [Fact]
    public void LogPath_reads_quoted_profiles_argument()
    {
        var path = LinuxProcesses.LogPath("/opt/arma/arma3server_x64", "-profiles=\"/srv/profiles/DevRun_ab\" -port=2402", Now);

        path.Should().Be(Path.Combine("/srv/profiles/DevRun_ab", "arma3server_x64_2026-11-10_17-05-09-042.rpt"));
    }

    [Fact]
    public void LogPath_falls_back_to_executable_directory()
    {
        var path = LinuxProcesses.LogPath("/opt/arma/arma3server_x64", "-port=2302", Now);

        path.Should().Be(Path.Combine(Path.GetDirectoryName("/opt/arma/arma3server_x64")!, "arma3server_x64_2026-11-10_17-05-09-042.rpt"));
    }

    [Fact]
    public void ShellScript_execs_executable_with_arguments_and_redirects_output()
    {
        var script = LinuxProcesses.ShellScript("/opt/arma/arma3server_x64", "-port=2302 -mod=\"@a;@b\"", "/srv/p/x.rpt");

        script.Should().Be("exec \"/opt/arma/arma3server_x64\" -port=2302 -mod=\"@a;@b\" < /dev/null >> \"/srv/p/x.rpt\" 2>&1");
    }

    [Fact]
    public void GetProcessesWithCommandLine_matches_executable_name_prefix_and_joins_cmdline()
    {
        WriteProcess("101", "/opt/arma/arma3server_x64\0-port=2302\0-profiles=/srv/p/Main\0");
        WriteProcess("102", "bash\0-c\0arma3server_x64\0");
        WriteProcess("104", "");
        WriteProcess("self", "/opt/arma/arma3server_x64\0");

        var result = LinuxProcesses.GetProcessesWithCommandLine("arma3server", _procRoot);

        result.Should().ContainSingle().Which.Should().Be(new ProcessCommandLineInfo(101, "/opt/arma/arma3server_x64 -port=2302 -profiles=/srv/p/Main"));
    }

    [Fact]
    public void GetProcessesWithCommandLine_skips_process_that_exited_mid_scan()
    {
        Directory.CreateDirectory(Path.Combine(_procRoot, "103"));

        LinuxProcesses.GetProcessesWithCommandLine("arma3server", _procRoot).Should().BeEmpty();
    }

    private void WriteProcess(string pid, string cmdline)
    {
        var directory = Path.Combine(_procRoot, pid);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "cmdline"), cmdline);
    }
}
