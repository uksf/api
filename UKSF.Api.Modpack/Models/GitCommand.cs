using UKSF.Api.Core;
using UKSF.Api.Core.Processes;

namespace UKSF.Api.Modpack.Models;

public class GitCommand(string workingDirectory, IUksfLogger logger)
{
    public GitCommand Execute(string command)
    {
        var processHelper = new ProcessRunner(logger, new CancellationTokenSource(), false);
        processHelper.Run(workingDirectory, "cmd.exe", $"/c \"git {command}\"", (int)TimeSpan.FromSeconds(30).TotalMilliseconds, true);
        return this;
    }

    public GitCommand ResetAndClean()
    {
        return Execute("reset --hard HEAD").Execute("clean -d -f");
    }

    public GitCommand Fetch()
    {
        return Execute("fetch");
    }

    public GitCommand Checkout(string reference)
    {
        return Execute($"checkout -t origin/{reference}").Execute($"checkout {reference}");
    }

    public GitCommand Pull()
    {
        return Execute("pull");
    }

    public async Task<GitCommand> Push(string reference)
    {
        await Task.Delay(TimeSpan.FromSeconds(5));
        return Execute($"push -u origin {reference}");
    }

    public GitCommand Commit(string message)
    {
        return Execute($"commit -am \"{message}\"");
    }

    public GitCommand Merge(string reference)
    {
        return Execute($"merge {reference}");
    }
}
