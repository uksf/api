using UKSF.Api.Core;
using UKSF.Api.Core.Processes;

namespace UKSF.Api.Modpack.Models;

public class GitCommand(string workingDirectory, IUksfLogger logger)
{
    public GitCommand Execute(string command, bool raiseErrors = true)
    {
        var processHelper = new ProcessRunner(logger, new CancellationTokenSource(), raiseErrors);
        processHelper.Run(workingDirectory, "cmd.exe", $"/c \"git {command}\"", (int)TimeSpan.FromSeconds(10).TotalMilliseconds, true);
        return this;
    }

    public GitCommand Fetch()
    {
        return Execute("fetch");
    }

    public GitCommand Checkout(string reference)
    {
        return Execute($"checkout {reference}");
    }

    public GitCommand TryCheckout(string reference)
    {
        return Execute($"checkout {reference}", false);
    }

    public GitCommand Pull()
    {
        return Execute("pull");
    }

    public GitCommand Push(string reference)
    {
        return Execute($"push -u origin {reference}");
    }

    public GitCommand Commit(string message)
    {
        return Execute($"commit -m {message}");
    }

    public GitCommand Merge(string reference)
    {
        return Execute($"merge {reference}");
    }
}
