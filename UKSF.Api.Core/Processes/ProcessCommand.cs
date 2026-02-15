using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using CliWrap;
using CliWrap.EventStream;
using UKSF.Api.Core.Extensions;

namespace UKSF.Api.Core.Processes;

/// <summary>
///     Represents a configurable and executable process command
/// </summary>
public class ProcessCommand(IUksfLogger logger, string executable, string workingDirectory, string arguments)
{
    private bool _enableInternalLogging;
    private string _processId;
    private IProcessTracker _processTracker;
    private TimeSpan _timeout = TimeSpan.FromMinutes(10);

    public ProcessCommand WithProcessId(string processId)
    {
        _processId = processId;
        return this;
    }

    public ProcessCommand WithTimeout(TimeSpan timeout)
    {
        _timeout = timeout;
        return this;
    }

    public ProcessCommand WithLogging(bool enableInternalLogging = false)
    {
        _enableInternalLogging = enableInternalLogging;
        return this;
    }

    public ProcessCommand WithProcessTracker(IProcessTracker processTracker)
    {
        _processTracker = processTracker;
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

        var logContext = $"'{executable}' in '{workingDirectory}' with '{arguments}'";
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
                    );
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
            CancellationToken.None
        );

        // Stream output as it becomes available
        // Use only the caller's cancellation token (not the combined timeout token) so that
        // process timeouts deliver error output lines instead of throwing OperationCanceledException.
        // User cancellation still propagates correctly via the original cancellationToken.
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
            var command = Cli.Wrap(executable).WithWorkingDirectory(workingDirectory).WithArguments(arguments).WithValidation(CommandResultValidation.None);

            await foreach (var cmdEvent in command.ListenAsync(cancellationToken))
            {
                switch (cmdEvent)
                {
                    case StartedCommandEvent started:
                        processId = started.ProcessId;
                        LogInformation($"Process started with ID {processId}");

                        // Register process with tracker if available
                        if (_processTracker != null && !string.IsNullOrEmpty(_processId))
                        {
                            var description = $"'{executable}' in '{workingDirectory}' with '{arguments}'";
                            _processTracker.RegisterProcess(processId, _processId, description);
                        }

                        break;

                    case StandardOutputCommandEvent stdOut when !string.IsNullOrEmpty(stdOut.Text):
                        await ProcessStandardOutputAsync(stdOut.Text, jsonParser, writer, cancellationToken); break;

                    case StandardErrorCommandEvent stdErr when !string.IsNullOrEmpty(stdErr.Text):
                        LogWarning($"Process error output: {stdErr.Text}");
                        await writer.WriteAsync(
                            new ProcessOutputLine
                            {
                                Content = stdErr.Text,
                                Type = ProcessOutputType.Error,
                                Exception = new Exception(stdErr.Text)
                            },
                            cancellationToken
                        );
                        break;

                    case ExitedCommandEvent exited:
                        exitCode = exited.ExitCode;
                        LogInformation($"Process exited with code {exited.ExitCode}");

                        // Unregister process from tracker if available
                        if (_processTracker != null && processId > 0)
                        {
                            _processTracker.UnregisterProcess(processId);
                        }

                        await writer.WriteAsync(
                            new ProcessOutputLine
                            {
                                Content = $"Process exited with code {exited.ExitCode}",
                                Type = ProcessOutputType.ProcessCompleted,
                                ExitCode = exited.ExitCode
                            },
                            cancellationToken
                        );
                        break;
                }
            }
        }
        finally
        {
            stopwatch.Stop();
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

    private void LogInformation(string message)
    {
        if (ShouldLog())
        {
            logger.LogInfo(message);
        }
    }

    private void LogWarning(string message)
    {
        if (ShouldLog())
        {
            logger.LogWarning(message);
        }
    }

    private void LogError(string message, Exception exception = null)
    {
        if (ShouldLog())
        {
            if (exception != null)
            {
                logger.LogError(message, exception);
            }
            else
            {
                logger.LogError(message);
            }
        }
    }

    private bool ShouldLog()
    {
        return _enableInternalLogging;
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

/// <summary>
///     Handles parsing of JSON output from build processes
/// </summary>
public class JsonOutputParser
{
    /// <summary>
    ///     Attempts to parse JSON output from a message line
    /// </summary>
    public bool TryParseJsonOutput(string message, out List<(string text, string color)> parsedMessages)
    {
        parsedMessages = [];

        if (string.IsNullOrEmpty(message) || message.Length <= 5 || !message.StartsWith("JSON"))
        {
            return false;
        }

        try
        {
            var parts = message.Split('}', '{');
            if (parts.Length < 2)
            {
                return false;
            }

            var json = $"{{{parts[1].Escape().Replace(@"\\n", "\\n")}}}";
            using var jsonDocument = JsonDocument.Parse(json);
            var root = jsonDocument.RootElement;

            var text = GetJsonProperty(root, "message");
            var color = GetJsonProperty(root, "colour");

            parsedMessages.Add((text, color));

            // Handle additional parts
            parsedMessages.AddRange(parts.Skip(2).Where(x => !string.IsNullOrEmpty(x)).Select(extraPart => (extraPart, "")));

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string GetJsonProperty(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) ? property.GetString() ?? "" : "";
    }
}
