using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
    public void SplitArguments_removes_quotes_and_keeps_quoted_whitespace()
    {
        LinuxProcesses.SplitArguments("-port=2302  -profiles=\"/srv/Arma Profiles/DevRun_ab\" -mod=\"@a;@b\" \"\" -password=pa$HOME")
                      .Should()
                      .Equal("-port=2302", "-profiles=/srv/Arma Profiles/DevRun_ab", "-mod=@a;@b", "", "-password=pa$HOME");
    }

    [Fact]
    public void LogPath_uses_profiles_argument_with_spaces()
    {
        var argv = LinuxProcesses.SplitArguments("-port=2302 -profiles=\"/srv/Arma Profiles/Main\" -config=x.cfg");

        LinuxProcesses.LogPath("/opt/arma/arma3server_x64", argv, Now)
                      .Should()
                      .Be(Path.Combine("/srv/Arma Profiles/Main", "arma3server_x64_2026-11-10_17-05-09-042.rpt"));
    }

    [Fact]
    public void LogPath_ignores_profiles_text_inside_another_argument()
    {
        var argv = LinuxProcesses.SplitArguments("-name=\"x -profiles=/wrong\" -port=2302");

        LinuxProcesses.LogPath("/opt/arma/arma3server_x64", argv, Now)
                      .Should()
                      .Be(Path.Combine(Path.GetDirectoryName("/opt/arma/arma3server_x64")!, "arma3server_x64_2026-11-10_17-05-09-042.rpt"));
    }

    [Fact]
    public void SetsidArguments_pass_log_executable_and_argv_as_separate_values()
    {
        LinuxProcesses.SetsidArguments("/opt/arma/arma3server_x64", ["-password=$(touch x)", "-port=2302"], "/srv/p/x.rpt", ProcessPriorityClass.Normal)
                      .Should()
                      .Equal(
                          "sh",
                          "-c",
                          "log=\"$1\"; shift; exec \"$@\" < /dev/null >> \"$log\" 2>&1",
                          "sh",
                          "/srv/p/x.rpt",
                          "/opt/arma/arma3server_x64",
                          "-password=$(touch x)",
                          "-port=2302"
                      );
    }

    [Fact]
    public void SetsidArguments_run_above_normal_priority_through_nice()
    {
        LinuxProcesses.SetsidArguments("/opt/arma/arma3server_x64", ["-port=2302"], "/srv/p/x.rpt", ProcessPriorityClass.AboveNormal)
                      .Skip(5)
                      .Should()
                      .Equal("nice", "-n", "-5", "/opt/arma/arma3server_x64", "-port=2302");
    }

    [Fact]
    public void LaunchDetached_passes_shell_metacharacters_literally()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Directory.CreateDirectory(_procRoot);
        var marker = Path.Combine(_procRoot, "marker");
        var printer = Path.Combine(_procRoot, "printargs");
        File.WriteAllText(printer, "#!/bin/sh\nfor a in \"$@\"; do echo \"[$a]\"; done\n");
        File.SetUnixFileMode(printer, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var profiles = Path.Combine(_procRoot, "my profiles");

        var pid = LinuxProcesses.LaunchDetached(printer, $"\"-password=$(touch {marker})\" -x=`id`;$HOME&* -profiles=\"{profiles}\"", ProcessPriorityClass.Normal, Now);
        Process.GetProcessById(pid).WaitForExit(5000);

        File.Exists(marker).Should().BeFalse();
        File.ReadAllText(Path.Combine(profiles, "printargs_2026-11-10_17-05-09-042.rpt"))
            .Should()
            .Be($"[-password=$(touch {marker})]\n[-x=`id`;$HOME&*]\n[-profiles={profiles}]\n");
    }

    [Fact]
    public void GetProcessesWithCommandLine_matches_executable_name_prefix_and_quotes_arguments_with_spaces()
    {
        WriteProcess("101", "/opt/arma/arma3server_x64\0-port=2302\0-profiles=/srv/Arma Profiles/Main\0");
        WriteProcess("102", "bash\0-c\0arma3server_x64\0");
        WriteProcess("104", "");
        WriteProcess("self", "/opt/arma/arma3server_x64\0");

        var result = LinuxProcesses.GetProcessesWithCommandLine("arma3server", _procRoot);

        result.Should()
              .ContainSingle()
              .Which.Should()
              .Be(new ProcessCommandLineInfo(101, "/opt/arma/arma3server_x64 -port=2302 \"-profiles=/srv/Arma Profiles/Main\""));
    }

    [Fact]
    public void GetProcessesWithCommandLine_keeps_healthy_processes_when_one_has_no_cmdline()
    {
        Directory.CreateDirectory(Path.Combine(_procRoot, "103"));
        WriteProcess("105", "/opt/arma/arma3server_x64\0-port=2302\0");

        LinuxProcesses.GetProcessesWithCommandLine("arma3server", _procRoot)
                      .Should()
                      .ContainSingle()
                      .Which.Should()
                      .Be(new ProcessCommandLineInfo(105, "/opt/arma/arma3server_x64 -port=2302"));
    }

    private void WriteProcess(string pid, string cmdline)
    {
        var directory = Path.Combine(_procRoot, pid);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "cmdline"), cmdline);
    }
}
