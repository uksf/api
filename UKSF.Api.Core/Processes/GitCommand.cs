using UKSF.Api.Core.Services;

namespace UKSF.Api.Core.Processes;

public class GitExitCodes
{
    public const int AlreadyOnBranch = 128;
}

public class GitCommandArgs
{
    public string WorkingDirectory { get; set; }
    public ErrorFilter ErrorFilter { get; set; } = new();
    public List<int> AllowedExitCodes { get; set; } = [];
    public bool Quiet { get; set; }
}

public class GitCommand(IGitService gitService)
{
    private readonly GitCommandArgs _gitCommandArgs = new();

    private CancellationToken _cancellationToken = CancellationToken.None;

    public GitCommand WithWorkingDirectory(string workingDirectory)
    {
        _gitCommandArgs.WorkingDirectory = workingDirectory;
        return this;
    }

    public GitCommand WithErrorExclusions(List<string> errorExclusions)
    {
        _gitCommandArgs.ErrorFilter.ErrorExclusions = errorExclusions;
        return this;
    }

    public GitCommand WithAllowedExitCodes(List<int> allowedExitCodes)
    {
        _gitCommandArgs.AllowedExitCodes = allowedExitCodes;
        return this;
    }

    public GitCommand WithQuiet(bool quiet)
    {
        _gitCommandArgs.Quiet = quiet;
        return this;
    }

    public GitCommand WithCancellationToken(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        return this;
    }

    public async Task<string> Execute(string command)
    {
        var result = await gitService.ExecuteCommand(_gitCommandArgs, command, _cancellationToken);
        _cancellationToken.ThrowIfCancellationRequested();
        return result;
    }
}
