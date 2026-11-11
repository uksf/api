using System.Diagnostics;
using System.Text;

namespace UKSF.Api.Core.Processes;

public static class LinuxProcesses
{
    private const string RedirectAndExec = "log=\"$1\"; shift; exec \"$@\" < /dev/null >> \"$log\" 2>&1";

    public static int LaunchDetached(string executable, string arguments, ProcessPriorityClass priority, DateTime now)
    {
        var workingDirectory = Path.GetDirectoryName(executable) ?? "/";
        var argv = SplitArguments(arguments);
        var logPath = LogPath(executable, argv, now);
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

        var startInfo = new ProcessStartInfo("setsid") { WorkingDirectory = workingDirectory, UseShellExecute = false };
        foreach (var argument in SetsidArguments(executable, argv, logPath, priority))
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo);
        return process?.Id ?? 0;
    }

    public static IReadOnlyList<string> SetsidArguments(string executable, IReadOnlyList<string> argv, string logPath, ProcessPriorityClass priority)
    {
        string[] command = [executable, .. argv];
        if (NiceValue(priority) is { } niceness)
        {
            command = ["nice", "-n", niceness.ToString(), .. command];
        }

        return ["sh", "-c", RedirectAndExec, "sh", logPath, .. command];
    }

    private static int? NiceValue(ProcessPriorityClass priority)
    {
        return priority switch
        {
            ProcessPriorityClass.Idle => 19,
            ProcessPriorityClass.BelowNormal => 10,
            ProcessPriorityClass.AboveNormal => -5,
            ProcessPriorityClass.High => -10,
            ProcessPriorityClass.RealTime => -20,
            _ => null
        };
    }

    public static IReadOnlyList<string> SplitArguments(string arguments)
    {
        List<string> result = [];
        var current = new StringBuilder();
        var inQuotes = false;
        var inArgument = false;
        foreach (var character in arguments ?? string.Empty)
        {
            if (character == '"')
            {
                inQuotes = !inQuotes;
                inArgument = true;
            }
            else if (char.IsWhiteSpace(character) && !inQuotes)
            {
                if (inArgument)
                {
                    result.Add(current.ToString());
                    current.Clear();
                    inArgument = false;
                }
            }
            else
            {
                current.Append(character);
                inArgument = true;
            }
        }

        if (inArgument)
        {
            result.Add(current.ToString());
        }

        return result;
    }

    public static string JoinArguments(IEnumerable<string> argv)
    {
        return string.Join(' ', argv.Select(argument => argument.Any(char.IsWhiteSpace) ? $"\"{argument}\"" : argument));
    }

    public static string LogPath(string executable, IReadOnlyList<string> argv, DateTime now)
    {
        var profiles = argv.LastOrDefault(argument => argument.StartsWith("-profiles=", StringComparison.OrdinalIgnoreCase));
        var directory = profiles is null ? Path.GetDirectoryName(executable) ?? "/" : profiles["-profiles=".Length..];
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
                var argv = File.ReadAllText(Path.Combine(directory, "cmdline")).Split('\0', StringSplitOptions.RemoveEmptyEntries);
                if (argv.Length == 0 || !Path.GetFileName(argv[0]).StartsWith(processNamePrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                results.Add(new ProcessCommandLineInfo(pid, JoinArguments(argv)));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        }

        return results;
    }
}
