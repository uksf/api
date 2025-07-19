using UKSF.Api.Core.Processes;

namespace UKSF.Api.Core.Services;

public interface IGitService
{
    GitCommand CreateGitCommand(string workingDirectory);
    Task<string> ExecuteCommand(string workingDirectory, string command, CancellationToken cancellationToken = default);
}

public class GitService(IProcessCommandFactory processCommandFactory, IUksfLogger logger) : IGitService
{
    private const string GitConfig = "-c core.askpass='' -c credential.helper='' -c core.longpaths=true";

    public GitCommand CreateGitCommand(string workingDirectory)
    {
        return new GitCommand(this, workingDirectory);
    }

    public async Task<string> ExecuteCommand(string workingDirectory, string command, CancellationToken cancellationToken = default)
    {
        try
        {
            var timeoutMs = (int)TimeSpan.FromMinutes(1).TotalMilliseconds;

            // Add git configurations to prevent hangs and credential prompts and prepend to command
            var fullCommand = $"git {GitConfig} {command}";

            var results = await RunProcess(workingDirectory, "cmd.exe", $"/c \"{fullCommand}\"", timeoutMs, cancellationToken);
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

    private async Task<List<string>> RunProcess(string workingDirectory, string executable, string args, int timeout, CancellationToken cancellationToken)
    {
        List<string> results = [];
        var errorFilter = new ErrorFilter(
            new ProcessErrorHandlingConfig
            {
                ErrorExclusions = [],
                IgnoreErrorGateOpen = "",
                IgnoreErrorGateClose = ""
            }
        );

        var command = processCommandFactory.CreateCommand(executable, workingDirectory, args).WithTimeout(TimeSpan.FromMilliseconds(timeout)).WithLogging();

        Exception delayedException = null;
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
                    var shouldIgnoreError = errorFilter.ShouldIgnoreError(outputLine.Content);
                    if (!shouldIgnoreError && outputLine.Exception != null)
                    {
                        delayedException = outputLine.Exception;
                    }

                    break;

                case ProcessOutputType.ProcessCompleted: processExitCode = outputLine.ExitCode; break;

                case ProcessOutputType.ProcessCancelled: throw new OperationCanceledException("Process execution was cancelled", outputLine.Exception);
            }
        }

        if (processExitCode != 0)
        {
            var exitCodeException = new Exception($"Process failed with exit code {processExitCode}");
            throw exitCodeException;
        }

        if (delayedException != null)
        {
            throw delayedException;
        }

        return results;
    }
}
