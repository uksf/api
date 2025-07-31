using UKSF.Api.Core.Processes;

namespace UKSF.Api.Core.Services;

public interface IGitService
{
    GitCommand CreateGitCommand();
    Task<string> ExecuteCommand(GitCommandArgs gitCommandArgs, string command, CancellationToken cancellationToken = default);
}

public class GitService(IProcessCommandFactory processCommandFactory, IUksfLogger logger) : IGitService
{
    private const string GitConfig = "-c core.askpass='' -c credential.helper='' -c core.longpaths=true";

    public GitCommand CreateGitCommand()
    {
        return new GitCommand(this);
    }

    public async Task<string> ExecuteCommand(GitCommandArgs gitCommandArgs, string command, CancellationToken cancellationToken = default)
    {
        try
        {
            var timeoutMs = (int)TimeSpan.FromMinutes(1).TotalMilliseconds;

            var fullCommand = $"git {GitConfig} {command}";
            if (gitCommandArgs.Quiet)
            {
                fullCommand += " --quiet";
            }

            var results = await RunProcess(gitCommandArgs, "cmd.exe", $"/c \"{fullCommand}\"", timeoutMs, cancellationToken);
            results = [.. results.Where(x => !x.Contains("Process exited"))];
            return results.Count > 0 ? results.Last() : string.Empty;
        }
        catch (OperationCanceledException)
        {
            // Re-throw cancellation exceptions to preserve cancellation behavior
            throw;
        }
        catch (Exception ex)
        {
            logger?.LogWarning($"Git command failed: {command}. Error: {ex.Message}");
            throw new Exception($"Git operation failed: {command}", ex);
        }
    }

    private async Task<List<string>> RunProcess(GitCommandArgs gitCommandArgs, string executable, string args, int timeout, CancellationToken cancellationToken)
    {
        List<string> results = [];

        var command = processCommandFactory.CreateCommand(executable, gitCommandArgs.WorkingDirectory, args).WithTimeout(TimeSpan.FromMilliseconds(timeout));

        Exception capturedException = null;
        var processExitCode = 0;

        await foreach (var outputLine in command.ExecuteAsync(cancellationToken))
        {
            results.Add(outputLine.Content);

            switch (outputLine.Type)
            {
                case ProcessOutputType.Output:
                    // Just collect output, don't log
                    break;

                case ProcessOutputType.Error:
                    var shouldIgnoreError = gitCommandArgs.ErrorFilter.ShouldIgnoreError(outputLine.Content);
                    if (!shouldIgnoreError && outputLine.Exception != null)
                    {
                        capturedException = outputLine.Exception;
                    }

                    break;

                case ProcessOutputType.ProcessCompleted: processExitCode = outputLine.ExitCode; break;

                case ProcessOutputType.ProcessCancelled: throw new OperationCanceledException("Process execution was cancelled", outputLine.Exception);
            }
        }

        if (!gitCommandArgs.AllowedExitCodes.Contains(processExitCode) && processExitCode != 0)
        {
            var exceptionMessage = $"Process failed with exit code {processExitCode}";
            if (capturedException != null)
            {
                exceptionMessage = $"{exceptionMessage}. {capturedException.Message}";
            }

            throw new Exception(exceptionMessage);
        }

        return results;
    }
}
