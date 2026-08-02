using UKSF.Api.Core.Processes;

namespace UKSF.Api.Backups.Services;

public class ProcessRunResult
{
    public int ExitCode { get; set; }
    public List<string> Output { get; set; } = [];
    public List<string> Errors { get; set; } = [];
}

public interface IProcessRunner
{
    Task<ProcessRunResult> Run(string executable, string workingDirectory, string arguments, TimeSpan timeout, CancellationToken cancellationToken = default);
}

public class ProcessRunner(IProcessCommandFactory processCommandFactory) : IProcessRunner
{
    public async Task<ProcessRunResult> Run(
        string executable,
        string workingDirectory,
        string arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    )
    {
        var command = processCommandFactory.CreateCommand(executable, workingDirectory, arguments).WithTimeout(timeout);
        var result = new ProcessRunResult();

        await foreach (var line in command.ExecuteAsync(cancellationToken))
        {
            switch (line.Type)
            {
                case ProcessOutputType.Output:           result.Output.Add(line.Content); break;
                case ProcessOutputType.Error:            result.Errors.Add(line.Content); break;
                case ProcessOutputType.ProcessCompleted: result.ExitCode = line.ExitCode; break;
                case ProcessOutputType.ProcessCancelled: throw new OperationCanceledException("Process execution was cancelled", line.Exception);
            }
        }

        return result;
    }
}
