using UKSF.Api.Core;
using UKSF.Api.Core.Processes;

namespace UKSF.Api.Modpack.Tests;

public static class ExitProcess
{
    public static ProcessCommand Command(IUksfLogger logger, string workingDirectory, int exitCode)
    {
        return OperatingSystem.IsWindows()
            ? new ProcessCommand(logger, "cmd.exe", workingDirectory, $"/c exit {exitCode}")
            : new ProcessCommand(logger, "sh", workingDirectory, $"-c \"exit {exitCode}\"");
    }

    public static ProcessCommand EchoThenExit(IUksfLogger logger, string workingDirectory, string output, int exitCode)
    {
        if (output is null)
        {
            return Command(logger, workingDirectory, exitCode);
        }

        return OperatingSystem.IsWindows()
            ? new ProcessCommand(logger, "cmd.exe", workingDirectory, $"/c echo {output}& exit {exitCode}")
            : new ProcessCommand(logger, "sh", workingDirectory, $"-c \"echo '{output}'; exit {exitCode}\"");
    }

    public static ProcessCommand WarnOnStderr(IUksfLogger logger, string workingDirectory, int exitCode)
    {
        return OperatingSystem.IsWindows()
            ? new ProcessCommand(logger, "cmd.exe", workingDirectory, $"/c \"echo lint warning 1>&2 & exit {exitCode}\"")
            : new ProcessCommand(logger, "sh", workingDirectory, $"-c \"echo lint warning >&2; exit {exitCode}\"");
    }
}
