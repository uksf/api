using System.Diagnostics;
using UKSF.Api.Core.Extensions;

namespace UKSF.Api.Core.Processes;

public class ProcessRunner(IUksfLogger logger, CancellationTokenSource cancellationTokenSource, bool raiseErrors = true, List<string> errorExclusions = null)
{
    private readonly CancellationTokenSource _errorCancellationTokenSource = new();
    private readonly ManualResetEvent _errorWaitHandle = new(false);
    private readonly ManualResetEvent _outputWaitHandle = new(false);
    private readonly List<string> _results = [];
    private CancellationTokenRegistration _cancellationTokenRegistration;
    private Exception _capturedException;
    private CancellationTokenRegistration _errorCancellationTokenRegistration;
    private string _logInfo;
    private Process _process;
    private bool _useLogger;

    public void Run(string workingDirectory, string executable, string args, int timeout, bool log = false)
    {
        _logInfo = $"'{executable}' in '{workingDirectory}' with '{args}'";
        _useLogger = log;

        _cancellationTokenRegistration = cancellationTokenSource.Token.Register(() =>
            {
                if (_useLogger)
                {
                    logger.LogInfo($"{_logInfo}: Process cancelled via token");
                }

                Kill();
            }
        );
        _errorCancellationTokenRegistration = _errorCancellationTokenSource.Token.Register(Kill);

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
            EnableRaisingEvents = false
        };

        _process.OutputDataReceived += OnOutputDataReceived;
        _process.ErrorDataReceived += OnErrorDataReceived;
        _process.Exited += (_, _) =>
        {
            if (_useLogger)
            {
                logger.LogWarning($"{_logInfo}: Process exited via event");
            }

            Kill();
        };

        _process.Start();
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        if (_process.WaitForExit(timeout) && _outputWaitHandle.WaitOne(timeout) && _errorWaitHandle.WaitOne(timeout))
        {
            if (_useLogger)
            {
                logger.LogInfo($"{_logInfo}: Process finished");
            }

            cancellationTokenSource.Token.ThrowIfCancellationRequested();

            if (_capturedException is not null)
            {
                if (_useLogger)
                {
                    logger.LogError($"{_logInfo}: Process captured exception", _capturedException);
                }

                if (raiseErrors)
                {
                    throw _capturedException;
                }
            }

            if (_process.ExitCode != 0 && raiseErrors)
            {
                if (_useLogger)
                {
                    logger.LogError($"{_logInfo}: Process exit code was non-zero ({_process.ExitCode})");
                }

                var message = _results.LastOrDefault() ?? "Process failed with unknown error";
                throw new Exception(message);
            }
        }
        else
        {
            // Process bombed out
            cancellationTokenSource.Token.ThrowIfCancellationRequested();

            var message = _results.LastOrDefault() ?? "Whoopsy poospy the program made an oopsy";
            Exception exception = new($"Process timed out and exited with non-zero code ({_process.ExitCode}) and last message ({message})");
            if (_useLogger)
            {
                logger.LogError($"{_logInfo}: Process bombed out", exception);
            }

            if (raiseErrors)
            {
                throw exception;
            }
        }

        if (_useLogger)
        {
            logger.LogWarning($"{_logInfo}: Process reached return");
        }
    }

    private void Kill()
    {
        if (_useLogger)
        {
            logger.LogWarning($"{_logInfo}: Process kill instructed");
        }

        if (_process is { HasExited: false })
        {
            _process?.Kill();
        }

        _outputWaitHandle?.Set();
        _errorWaitHandle?.Set();

        _cancellationTokenRegistration.Dispose();
        _errorCancellationTokenRegistration.Dispose();
    }

    private void OnOutputDataReceived(object sender, DataReceivedEventArgs receivedEventArgs)
    {
        if (receivedEventArgs.Data == null)
        {
            _outputWaitHandle.Set();
            return;
        }

        var data = receivedEventArgs.Data;
        if (!string.IsNullOrEmpty(data))
        {
            _results.Add(data);
        }
    }

    private void OnErrorDataReceived(object sender, DataReceivedEventArgs receivedEventArgs)
    {
        if (receivedEventArgs.Data == null)
        {
            _errorWaitHandle.Set();
            return;
        }

        var data = receivedEventArgs.Data;
        if (string.IsNullOrEmpty(data))
        {
            if (_useLogger)
            {
                logger.LogWarning($"{_logInfo}: Process received error: {data}");
            }

            return;
        }

        if (errorExclusions is not null && errorExclusions.Any(x => data.ContainsIgnoreCase(x)))
        {
            return;
        }

        _capturedException = new Exception(data);
        _errorCancellationTokenSource.Cancel();
    }
}
