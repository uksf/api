using System.Diagnostics;
using System.Text.RegularExpressions;

namespace UKSF.Api.Core.Processes;

public static partial class LinuxProcesses
{
    public static int LaunchDetached(string executable, string arguments, DateTime now)
    {
        var workingDirectory = Path.GetDirectoryName(executable) ?? "/";
        var logPath = LogPath(executable, arguments, now);
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

        var startInfo = new ProcessStartInfo("setsid") { WorkingDirectory = workingDirectory, UseShellExecute = false };
        startInfo.ArgumentList.Add("sh");
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(ShellScript(executable, arguments, logPath));
        using var process = Process.Start(startInfo);
        return process?.Id ?? 0;
    }

    public static string ShellScript(string executable, string arguments, string logPath)
    {
        return $"exec \"{executable}\" {arguments} < /dev/null >> \"{logPath}\" 2>&1";
    }

    public static string LogPath(string executable, string arguments, DateTime now)
    {
        var match = ProfilesArgument().Match(arguments ?? string.Empty);
        var directory = match.Success ? match.Groups[1].Value : Path.GetDirectoryName(executable) ?? "/";
        var name = Path.GetFileNameWithoutExtension(executable);
        return Path.Combine(directory, $"{name}_{now:yyyy-MM-dd_HH-mm-ss-fff}.rpt");
    }

    public static IReadOnlyList<ProcessCommandLineInfo> GetProcessesWithCommandLine(string processNamePrefix, string procRoot = "/proc")
    {
        List<ProcessCommandLineInfo> results = [];
        foreach (var directory in Directory.EnumerateDirectories(procRoot))
        {
            if (!int.TryParse(Path.GetFileName(directory), out var pid))
            {
                continue;
            }

            try
            {
                var arguments = File.ReadAllText(Path.Combine(directory, "cmdline")).Split('\0', StringSplitOptions.RemoveEmptyEntries);
                if (arguments.Length == 0 || !Path.GetFileName(arguments[0]).StartsWith(processNamePrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                results.Add(new ProcessCommandLineInfo(pid, string.Join(' ', arguments)));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        }

        return results;
    }

    [GeneratedRegex("""-profiles="?([^"\s]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex ProfilesArgument();
}
