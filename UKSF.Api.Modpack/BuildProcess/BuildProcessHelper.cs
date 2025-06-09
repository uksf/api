using System.Diagnostics;
using System.Text.Json.Nodes;
using UKSF.Api.Core;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Modpack.BuildProcess;

public class BuildProcessHelper(
    IStepLogger stepLogger,
    IUksfLogger logger,
    CancellationTokenSource cancellationTokenSource,
    IVariablesService variablesService,
    IBuildProcessTracker processTracker,
    bool suppressOutput = false,
    bool raiseErrors = true,
    bool errorSilently = false,
    List<string> errorExclusions = null,
    string ignoreErrorGateClose = "",
    string ignoreErrorGateOpen = "",
    string buildId = null
) : IDisposable
{
    private readonly CancellationTokenSource _errorCancellationTokenSource = new();
    private readonly ManualResetEvent _errorWaitHandle = new(false);
    private readonly object _killLock = new();
    private readonly ManualResetEvent _outputWaitHandle = new(false);
    private readonly List<string> _results = [];
    private CancellationTokenRegistration _cancellationTokenRegistration;
    private Exception _capturedException;
    private bool _disposed;
    private CancellationTokenRegistration _errorCancellationTokenRegistration;
    private volatile bool _errorStreamClosed;
    private int _exitCode = int.MinValue;
    private bool _externalCancellationRequested;
    private bool _ignoreErrors;
    private bool _isKilling;
    private string _logInfo;
    private volatile bool _outputStreamClosed;
    private Process _process;
    private bool _useLogger;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public List<string> Run(string workingDirectory, string executable, string args, int timeout, bool log = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(BuildProcessHelper));

        _logInfo = $"'{executable}' in '{workingDirectory}' with '{args}'";
        _useLogger = log;

        Log($"Starting process: {_logInfo}");

        SetupCancellationTokens();
        StartProcess(workingDirectory, executable, args);
        RegisterProcessForTracking();
        BeginAsyncReading();

        return WaitForCompletion(timeout);
    }

    private void SetupCancellationTokens()
    {
        _cancellationTokenRegistration = cancellationTokenSource.Token.Register(() =>
            {
                Log("Build process cancelled via token");
                _externalCancellationRequested = true;
                Kill();
            }
        );

        _errorCancellationTokenRegistration = _errorCancellationTokenSource.Token.Register(Kill);
    }

    private void StartProcess(string workingDirectory, string executable, string args)
    {
        _process = new Process
        {
            StartInfo =
            {
                FileName = executable,
                WorkingDirectory = workingDirectory,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            },
            EnableRaisingEvents = true
        };

        _process.OutputDataReceived += OnOutputDataReceived;
        _process.ErrorDataReceived += OnErrorDataReceived;
        _process.Exited += (_, _) =>
        {
            Log("Process exited via event");
            Kill();
        };

        _process.Start();
        Log($"Started process with ID {_process.Id}");
    }

    private void RegisterProcessForTracking()
    {
        if (!string.IsNullOrEmpty(buildId))
        {
            processTracker.RegisterProcess(_process.Id, buildId, _process.StartInfo.Arguments);
            Log($"Registered build process {_process.Id} for build {buildId}: {_process.StartInfo.Arguments}");
        }
    }

    private void BeginAsyncReading()
    {
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
    }

    private List<string> WaitForCompletion(int timeout)
    {
        var processExited = _process.WaitForExit(timeout);
        var outputProcessed = _outputWaitHandle.WaitOne(timeout);
        var errorProcessed = _errorWaitHandle.WaitOne(timeout);

        Log($"Process status - Exited: {processExited}, OutputProcessed: {outputProcessed}, ErrorProcessed: {errorProcessed}");

        // Even if process exited, ensure all async output is processed
        if (processExited)
        {
            // Give additional time for async output processing to complete
            var additionalWaitTime = Math.Min(timeout / 10, 2000); // Up to 2 seconds additional wait
            if (!outputProcessed)
            {
                Log($"Process exited but output not fully processed, waiting additional {additionalWaitTime}ms");
                outputProcessed = _outputWaitHandle.WaitOne(additionalWaitTime);
            }

            if (!errorProcessed)
            {
                Log($"Process exited but error output not fully processed, waiting additional {additionalWaitTime}ms");
                errorProcessed = _errorWaitHandle.WaitOne(additionalWaitTime);
            }

            // Final fallback: if streams still haven't closed naturally but process has exited, 
            // force completion to prevent deadlocks
            if (!outputProcessed || !errorProcessed)
            {
                Log($"Process exited but streams not closed (Output: {_outputStreamClosed}, Error: {_errorStreamClosed}), forcing completion");
                ForceStreamCompletion();
                outputProcessed = true;
                errorProcessed = true;
            }
        }

        if (processExited && outputProcessed && errorProcessed)
        {
            return HandleSuccessfulCompletion();
        }

        return HandleTimeout(processExited, outputProcessed, errorProcessed, timeout);
    }

    private void ForceStreamCompletion()
    {
        Log("Forcing stream completion to prevent deadlock");

        if (!_outputStreamClosed)
        {
            Log("Output stream not closed naturally, forcing completion");
            _outputStreamClosed = true;
            _outputWaitHandle.Set();
        }

        if (!_errorStreamClosed)
        {
            Log("Error stream not closed naturally, forcing completion");
            _errorStreamClosed = true;
            _errorWaitHandle.Set();
        }
    }

    private List<string> HandleSuccessfulCompletion()
    {
        Log("Process finished successfully");

        // Only exit early if cancellation was explicitly requested externally, not due to internal cleanup
        if (_externalCancellationRequested)
        {
            Log("Process was cancelled externally but completed successfully");
            return _results;
        }

        if (_capturedException != null)
        {
            LogError("Process captured exception", _capturedException);

            if (raiseErrors)
            {
                throw _capturedException;
            }

            if (!errorSilently)
            {
                stepLogger.LogError(_capturedException);
            }
        }

        if (_exitCode != 0 && _exitCode != int.MinValue && raiseErrors)
        {
            var errorMessage = GetErrorMessageFromResults();
            LogError($"Process exit code was non-zero ({_exitCode})");
            throw new Exception(errorMessage);
        }

        // Give a brief moment for any final log processing to complete
        if (!suppressOutput && _results.Count > 0)
        {
            Log("Allowing final log processing to complete");
            Thread.Sleep(100); // Brief pause for log processing
        }

        Log("Process reached successful return");
        return _results;
    }

    private List<string> HandleTimeout(bool processExited, bool outputProcessed, bool errorProcessed, int timeout)
    {
        // Check if cancellation was requested and handle gracefully
        if (_externalCancellationRequested)
        {
            Log("Process was cancelled externally during timeout handling");
            return _results;
        }

        var lastMessage = GetLastMessageFromResults();
        var timeoutMessage = GetTimeoutMessage(processExited, outputProcessed);
        var fullMessage = $"{timeoutMessage} after {timeout}ms. Exit code: {_exitCode}, Last message: {lastMessage}";

        LogWarning(
            $"Process timeout/failure - ProcessExited: {processExited}, OutputProcessed: {outputProcessed}, ErrorProcessed: {errorProcessed}, HasExited: {_process?.HasExited}, Timeout: {timeout}ms"
        );

        var exception = new Exception(fullMessage);
        LogError("Process failed", exception);

        if (raiseErrors)
        {
            throw exception;
        }

        if (!errorSilently)
        {
            stepLogger.LogError(exception);
        }

        Log("Process reached error return");
        return _results;
    }

    private string GetErrorMessageFromResults()
    {
        if (_results.Count == 0)
        {
            return $"Process failed with exit code {_exitCode}";
        }

        var json = "";
        var messages = ExtractMessages(_results.Last(), ref json);
        return messages.Count != 0 ? messages.First().Item1 : $"Process failed with exit code {_exitCode}";
    }

    private string GetLastMessageFromResults()
    {
        if (_results.Count == 0)
        {
            return "No output received";
        }

        var json = "";
        var messages = ExtractMessages(_results.Last(), ref json);
        return messages.FirstOrDefault()?.Item1 ?? "Process failed with unknown error";
    }

    private static string GetTimeoutMessage(bool processExited, bool outputProcessed)
    {
        return !processExited ? "Process execution timed out" :
            !outputProcessed  ? "Output processing timed out" : "Error processing timed out";
    }

    private void Kill()
    {
        if (_disposed)
        {
            return;
        }

        lock (_killLock)
        {
            if (_isKilling)
            {
                Log("Kill already in progress, skipping duplicate call");
                return;
            }

            _isKilling = true;
        }

        LogWarning("Kill process instructed");

        try
        {
            KillProcessTree();
        }
        catch (Exception ex)
        {
            LogError("Error killing process", ex);
        }
        finally
        {
            CleanupResources();
        }
    }

    private void KillProcessTree()
    {
        if (_process is { HasExited: false })
        {
            LogWarning($"Attempting to kill process {_process.Id} and its children");

            try
            {
                _process.Kill(true);
                Log($"Kill command sent to process {_process.Id}");
            }
            catch (Exception killEx)
            {
                LogError($"Failed to send kill command to process {_process.Id}", killEx);
                return;
            }

            if (!_process.WaitForExit(5000))
            {
                LogError($"Process {_process.Id} did not exit after 5 seconds, may be frozen");

                try
                {
                    _process.Kill(true);
                    Log("Second kill attempt made");

                    if (!_process.WaitForExit(3000))
                    {
                        LogError($"Process {_process.Id} still not exited after second kill attempt");
                    }
                }
                catch (Exception ex2)
                {
                    LogError("Failed second kill attempt", ex2);
                }
            }
            else
            {
                Log($"Process {_process.Id} killed successfully");
            }
        }
        else if (_process?.HasExited == true)
        {
            Log($"Process {_process.Id} has already exited");
        }
        else
        {
            Log("No process to kill");
        }
    }

    private void CleanupResources()
    {
        Log("Starting cleanup resources");

        // Ensure streams are marked as closed and wait handles are set
        ForceStreamCompletion();

        _cancellationTokenRegistration.Dispose();
        _errorCancellationTokenRegistration.Dispose();

        if (_process != null)
        {
            StoreExitCode();
            UnregisterProcessFromTracking();
            DisposeProcess();
        }

        Log("Cleanup resources completed");
    }

    private void StoreExitCode()
    {
        if (_process.HasExited)
        {
            _exitCode = _process.ExitCode;
            Log($"Stored exit code {_exitCode}");
        }
        else
        {
            LogWarning("Process has not exited during cleanup");
        }
    }

    private void UnregisterProcessFromTracking()
    {
        processTracker.UnregisterProcess(_process.Id);
        Log($"Unregistered build process {_process.Id}");
    }

    private void DisposeProcess()
    {
        _process.OutputDataReceived -= OnOutputDataReceived;
        _process.ErrorDataReceived -= OnErrorDataReceived;
        _process.Dispose();
        _process = null;
    }

    private void OnOutputDataReceived(object sender, DataReceivedEventArgs receivedEventArgs)
    {
        if (receivedEventArgs.Data == null)
        {
            Log("Output stream closed, setting output wait handle");
            _outputStreamClosed = true;
            _outputWaitHandle.Set();
            return;
        }

        var data = receivedEventArgs.Data;
        if (!string.IsNullOrEmpty(data))
        {
            _results.Add(data);
        }

        if (!suppressOutput)
        {
            LogMessagesFromData(data);
        }
    }

    private void OnErrorDataReceived(object sender, DataReceivedEventArgs receivedEventArgs)
    {
        if (receivedEventArgs.Data == null)
        {
            Log("Error stream closed, setting error wait handle");
            _errorStreamClosed = true;
            _errorWaitHandle.Set();
            return;
        }

        var data = receivedEventArgs.Data;
        if (string.IsNullOrEmpty(data))
        {
            return;
        }

        LogWarning($"Process received error data: {data}");

        if (ShouldIgnoreError(data))
        {
            return;
        }

        _capturedException = new Exception(data);
        _errorCancellationTokenSource.Cancel();
    }

    private bool ShouldIgnoreError(string data)
    {
        if (SkipForIgnoreErrorGate(data))
        {
            return true;
        }

        if (errorExclusions?.Any(data.ContainsIgnoreCase) == true)
        {
            Log($"Ignoring excluded error: {data}");
            return true;
        }

        return false;
    }

    private bool SkipForIgnoreErrorGate(string data)
    {
        if (data.ContainsIgnoreCase(ignoreErrorGateClose))
        {
            _ignoreErrors = false;
            return true;
        }

        if (_ignoreErrors)
        {
            return true;
        }

        if (data.ContainsIgnoreCase(ignoreErrorGateOpen))
        {
            _ignoreErrors = true;
            return true;
        }

        return false;
    }

    private void LogMessagesFromData(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        var json = "";
        try
        {
            var messages = ExtractMessages(message, ref json);
            foreach (var (text, colour) in messages)
            {
                stepLogger.Log(text, colour);
            }
        }
        catch (Exception exception)
        {
            _capturedException = new Exception($"Json failed: {json}\n\nMessage: {message}\n\n{exception}");
            LogError("Failed to process json", _capturedException);
            _errorCancellationTokenSource.Cancel();
        }
    }

    private static List<Tuple<string, string>> ExtractMessages(string message, ref string json)
    {
        List<Tuple<string, string>> messages = [];
        if (message.Length > 5 && message[..4] == "JSON")
        {
            var parts = message.Split('}', '{');
            json = $"{{{parts[1].Escape().Replace(@"\\n", "\\n")}}}";
            var jsonObject = JsonNode.Parse(json);
            messages.Add(new Tuple<string, string>(jsonObject.GetValueFromObject("message"), jsonObject.GetValueFromObject("colour")));
            messages.AddRange(parts.Skip(2).Where(x => !string.IsNullOrEmpty(x)).Select(extra => new Tuple<string, string>(extra, "")));
        }
        else
        {
            messages.Add(new Tuple<string, string>(message, ""));
        }

        return messages;
    }

    // Centralized logging methods
    private void Log(string message)
    {
        if (ShouldLog())
        {
            logger.LogInfo($"{_logInfo}: {message}");
        }
    }

    private void LogWarning(string message)
    {
        if (ShouldLog())
        {
            logger.LogWarning($"{_logInfo}: {message}");
        }
    }

    private void LogError(string message, Exception exception = null)
    {
        if (ShouldLog())
        {
            if (exception != null)
            {
                logger.LogError($"{_logInfo}: {message}", exception);
            }
            else
            {
                logger.LogError($"{_logInfo}: {message}");
            }
        }
    }

    private bool ShouldLog()
    {
        var forceLogsVariable = variablesService.GetVariable("BUILD_FORCE_LOGS");
        var forceLogs = forceLogsVariable?.AsBoolWithDefault(false) ?? false;
        return _useLogger || forceLogs;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            Kill();
            _errorCancellationTokenSource.Dispose();
            _errorWaitHandle.Dispose();
            _outputWaitHandle.Dispose();
            _cancellationTokenRegistration.Dispose();
            _errorCancellationTokenRegistration.Dispose();
        }

        _disposed = true;
    }
}
