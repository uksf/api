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
}
