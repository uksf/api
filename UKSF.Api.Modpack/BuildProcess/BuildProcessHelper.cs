using System.Diagnostics;
using System.Text.Json.Nodes;
using UKSF.Api.Core;
using UKSF.Api.Core.Extensions;

namespace UKSF.Api.Modpack.BuildProcess;

public class BuildProcessHelper(
    IStepLogger stepLogger,
    IUksfLogger logger,
    CancellationTokenSource cancellationTokenSource,
    bool suppressOutput = false,
    bool raiseErrors = true,
    bool errorSilently = false,
    List<string> errorExclusions = null,
    string ignoreErrorGateClose = "",
    string ignoreErrorGateOpen = ""
)
{
    private readonly CancellationTokenSource _errorCancellationTokenSource = new();
    private readonly ManualResetEvent _errorWaitHandle = new(false);
    private readonly ManualResetEvent _outputWaitHandle = new(false);
    private readonly List<string> _results = [];
    private CancellationTokenRegistration _cancellationTokenRegistration;
    private Exception _capturedException;
    private CancellationTokenRegistration _errorCancellationTokenRegistration;
    private bool _ignoreErrors;
    private Process _process;
    private string _logInfo;
    private bool _useLogger;

    public List<string> Run(string workingDirectory, string executable, string args, int timeout, bool log = false)
    {
        _logInfo = $"'{executable}' in '{workingDirectory}' with '{args}'";
        _useLogger = log;

        _cancellationTokenRegistration = cancellationTokenSource.Token.Register(
            () =>
            {
                if (_useLogger)
                {
                    logger.LogInfo($"{_logInfo}: Build process cancelled via token");
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
                logger.LogWarning($"{_logInfo}: Build process exited via event");
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
                logger.LogInfo($"{_logInfo}: Build process finished");
            }

            cancellationTokenSource.Token.ThrowIfCancellationRequested();

            if (_capturedException is not null)
            {
                if (_useLogger)
                {
                    logger.LogError($"{_logInfo}: Build process captured exception", _capturedException);
                }

                if (raiseErrors)
                {

                    throw _capturedException;
                }

                if (!errorSilently)
                {
                    stepLogger.LogError(_capturedException);
                }
            }

            if (_process.ExitCode != 0 && raiseErrors)
            {
                if (_useLogger)
                {
                    logger.LogError($"{_logInfo}: Build process exit code was non-zero ({_process.ExitCode})");
                }

                var json = "";
                var messages = ExtractMessages(_results.Last(), ref json);
                if (messages.Count != 0)
                {
                    throw new Exception(messages.First().Item1);
                }

                throw new Exception();
            }
        }
        else
        {
            // Process bombed out
            cancellationTokenSource.Token.ThrowIfCancellationRequested();

            var json = "";
            var messages = ExtractMessages(_results.Last(), ref json);
            var lastMessage = messages.FirstOrDefault()?.Item1 ?? "Woopsy Poospy the program made an Oopsy";

            Exception exception = new($"Process timed out and exited with non-zero code ({_process.ExitCode}) and last message ({lastMessage})");
            if (_useLogger)
            {
                logger.LogError($"{_logInfo}: Build process bombed out", exception);
            }

            if (raiseErrors)
            {
                throw exception;
            }

            if (!errorSilently)
            {
                stepLogger.LogError(exception);
            }
        }

        if (_useLogger)
        {
            logger.LogWarning($"{_logInfo}: Build process reached return");
        }

        return _results;
    }

    private void Kill()
    {
        if (_useLogger)
        {
            logger.LogWarning($"{_logInfo}: Build process kill instructed");
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

        if (!suppressOutput)
        {
            LogMessagesFromData(data);
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
        if (string.IsNullOrEmpty(data) || SkipForIgnoreErrorGate(data))
        {
            if (_useLogger)
            {
                logger.LogWarning($"{_logInfo}: Build process received error: {data}");
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
            _capturedException = new Exception($"Json failed: {json}\n\n{exception}");
            if (_useLogger)
            {
                logger.LogError($"{_logInfo}: Build process failed to process json", _capturedException);
            }

            _errorCancellationTokenSource.Cancel();
        }
    }

    private static List<Tuple<string, string>> ExtractMessages(string message, ref string json)
    {
        List<Tuple<string, string>> messages = [];
        if (message.Length > 5 && message[..4] == "JSON")
        {
            var parts = message.Split('}', '{'); // covers cases where buffer gets extra data flushed to it after the closing brace
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
}
