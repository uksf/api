using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using CliWrap;
using CliWrap.EventStream;
using UKSF.Api.Core;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Modpack.BuildProcess.Modern;

/// <summary>
///     Simple and clean process executor using command pattern with builder
/// </summary>
public class BuilderProcessExecutor(IUksfLogger logger, IVariablesService variablesService, IBuildProcessTracker processTracker)
{
    /// <summary>
    ///     Creates a new process command builder
    /// </summary>
    public ProcessCommandBuilder CreateCommand(string executable, string workingDirectory, string arguments)
    {
        return new ProcessCommandBuilder(logger, variablesService, processTracker, executable, workingDirectory, arguments);
    }
}

/// <summary>
///     Builder for configuring and executing process commands
/// </summary>
public class ProcessCommandBuilder
{
    private readonly string _arguments;
    private readonly string _executable;
    private readonly IUksfLogger _logger;
    private readonly IBuildProcessTracker _processTracker;
    private readonly IVariablesService _variablesService;
    private readonly string _workingDirectory;
    private string _buildId;
    private bool _enableInternalLogging;
    private TimeSpan _timeout = TimeSpan.FromMinutes(10);

    internal ProcessCommandBuilder(
        IUksfLogger logger,
        IVariablesService variablesService,
        IBuildProcessTracker processTracker,
        string executable,
        string workingDirectory,
        string arguments
    )
    {
        _executable = executable;
        _workingDirectory = workingDirectory;
        _arguments = arguments;
        _logger = logger;
        _variablesService = variablesService;
        _processTracker = processTracker;
    }

    public ProcessCommandBuilder WithBuildId(string buildId)
    {
        _buildId = buildId;
        return this;
    }

    public ProcessCommandBuilder WithTimeout(TimeSpan timeout)
    {
        _timeout = timeout;
        return this;
    }

    public ProcessCommandBuilder WithLogging(bool enableInternalLogging = false)
    {
        _enableInternalLogging = enableInternalLogging;
        return this;
    }

    /// <summary>
    ///     Executes the command and streams output in real-time
    /// </summary>
    public async IAsyncEnumerable<ProcessOutputLine> ExecuteAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Create channel for streaming output
        var channel = Channel.CreateUnbounded<ProcessOutputLine>();
        var writer = channel.Writer;
        var reader = channel.Reader;

        // Create timeout cancellation token and combine with provided token
        using var timeoutCts = new CancellationTokenSource(_timeout);
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var combinedToken = combinedCts.Token;

        var logContext = $"'{_executable}' in '{_workingDirectory}' with '{_arguments}'";
        LogInformation($"Starting process: {logContext} (timeout: {_timeout})");

        // Capture the timeout token to avoid disposal issues
        var timeoutToken = timeoutCts.Token;

        // Start the process execution in a background task
        var executionTask = Task.Run(
            async () =>
            {
                try
                {
                    await ExecuteProcessAsync(writer, combinedToken);
                }
                catch (OperationCanceledException) when (timeoutToken.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    // Handle timeout specifically
                    LogWarning($"Process execution timed out after {_timeout}");
                    await writer.WriteAsync(
                        new ProcessOutputLine
                        {
                            Content = $"Process execution timed out after {_timeout}",
                            Type = ProcessOutputType.Error,
                            Exception = new TimeoutException($"Process execution timed out after {_timeout}")
                        },
                        CancellationToken.None
                    ); // Use None to ensure this gets written
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Handle external cancellation
                    LogWarning("Process execution was cancelled");
                    await writer.WriteAsync(
                        new ProcessOutputLine
                        {
                            Content = "Process execution was cancelled",
                            Type = ProcessOutputType.ProcessCancelled,
                            Exception = new OperationCanceledException("Process execution was cancelled")
                        },
                        CancellationToken.None
                    );
                }
                catch (Exception ex)
                {
                    LogError($"Process execution failed: {ex.Message}", ex);
                    await writer.WriteAsync(
                        new ProcessOutputLine
                        {
                            Content = $"Process execution failed: {ex.Message}",
                            Type = ProcessOutputType.Error,
                            Exception = ex
                        },
                        CancellationToken.None
                    );
                }
                finally
                {
                    writer.TryComplete();
                }
            },
            CancellationToken.None // Don't cancel the task itself, let it handle cancellation internally
        );

        // Stream output as it becomes available - use only external cancellation token
        // Don't use timeout token here as timeout is handled in the background task
        // ReSharper disable once PossiblyMistakenUseOfCancellationToken
        await foreach (var outputLine in reader.ReadAllAsync(cancellationToken))
        {
            yield return outputLine;
        }

        // Wait for execution task to complete
        await executionTask;
    }

    private async Task ExecuteProcessAsync(ChannelWriter<ProcessOutputLine> writer, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var jsonParser = new JsonOutputParser();
        var processId = 0;
        var exitCode = 0;

        try
        {
            var command = Cli.Wrap(_executable).WithWorkingDirectory(_workingDirectory).WithArguments(_arguments).WithValidation(CommandResultValidation.None);

            await foreach (var cmdEvent in command.ListenAsync(cancellationToken))
            {
                switch (cmdEvent)
                {
                    case StartedCommandEvent started:
                        processId = started.ProcessId;
                        RegisterProcess(_buildId, processId, _arguments);
                        LogInformation($"Process started with ID {processId}");
                        break;

                    case StandardOutputCommandEvent stdOut when !string.IsNullOrEmpty(stdOut.Text):
                        await ProcessStandardOutputAsync(stdOut.Text, jsonParser, writer, cancellationToken); break;

                    case StandardErrorCommandEvent stdErr when !string.IsNullOrEmpty(stdErr.Text):
                        LogWarning($"Process error output: {stdErr.Text}");
                        await writer.WriteAsync(new ProcessOutputLine { Content = stdErr.Text, Type = ProcessOutputType.Error }, cancellationToken);
                        break;

                    case ExitedCommandEvent exited:
                        exitCode = exited.ExitCode;
                        LogInformation($"Process exited with code {exited.ExitCode}");

                        // Send process completion information to the caller
                        await writer.WriteAsync(
                            new ProcessOutputLine { Type = ProcessOutputType.ProcessCompleted, ExitCode = exited.ExitCode },
                            cancellationToken
                        );
                        break;
                }
            }
        }

        finally
        {
            stopwatch.Stop();
            UnregisterProcess(processId);
            LogInformation($"Process completed in {stopwatch.Elapsed:mm\\:ss\\.fff} with exit code {exitCode}");
        }
    }

    private async Task ProcessStandardOutputAsync(
        string data,
        JsonOutputParser jsonParser,
        ChannelWriter<ProcessOutputLine> writer,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrEmpty(data))
        {
            return;
        }

        var lines = data.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine))
            {
                continue;
            }

            // Always provide the output content for collection
            if (jsonParser.TryParseJsonOutput(trimmedLine, out var parsedMessages))
            {
                foreach (var (text, color) in parsedMessages)
                {
                    await writer.WriteAsync(
                        new ProcessOutputLine
                        {
                            Content = text,
                            Type = ProcessOutputType.Output,
                            Color = color,
                            IsJson = true
                        },
                        cancellationToken
                    );
                }
            }
            else
            {
                await writer.WriteAsync(new ProcessOutputLine { Content = trimmedLine, Type = ProcessOutputType.Output }, cancellationToken);
            }
        }
    }

    private void RegisterProcess(string buildId, int processId, string arguments)
    {
        if (!string.IsNullOrEmpty(buildId))
        {
            _processTracker.RegisterProcess(processId, buildId, arguments);
            LogInformation($"Registered build process {processId} for build {buildId}: {arguments}");
        }
    }

    private void UnregisterProcess(int processId)
    {
        if (processId > 0)
        {
            _processTracker.UnregisterProcess(processId);
            LogInformation($"Unregistered build process {processId}");
        }
    }

    private void LogInformation(string message)
    {
        if (ShouldLog())
        {
            _logger.LogInfo(message);
        }
    }

    private void LogWarning(string message)
    {
        if (ShouldLog())
        {
            _logger.LogWarning(message);
        }
    }

    private void LogError(string message, Exception exception = null)
    {
        if (ShouldLog())
        {
            if (exception != null)
            {
                _logger.LogError(message, exception);
            }
            else
            {
                _logger.LogError(message);
            }
        }
    }

    private bool ShouldLog()
    {
        if (_enableInternalLogging)
        {
            return true;
        }

        var forceLogsVariable = _variablesService.GetVariable("BUILD_FORCE_LOGS");
        return forceLogsVariable?.AsBoolWithDefault(false) ?? false;
    }
}

/// <summary>
///     Represents a line of output from the process with metadata
/// </summary>
public class ProcessOutputLine
{
    public ProcessOutputType Type { get; init; } = ProcessOutputType.Output;
    public string Content { get; init; }
    public string Color { get; init; }
    public bool IsJson { get; init; }
    public Exception Exception { get; set; }
    public int ExitCode { get; init; }
}

/// <summary>
///     Type of process output
/// </summary>
public enum ProcessOutputType
{
    Output,
    Error,
    ProcessCompleted,
    ProcessCancelled
}
