using System.Diagnostics;
using System.Text.Json.Nodes;
using UKSF.Api.Shared.Extensions;

namespace UKSF.Api.Modpack.Services.BuildProcess;

public class BuildProcessHelper
{
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly CancellationTokenSource _errorCancellationTokenSource = new();
    private readonly List<string> _errorExclusions;
    private readonly bool _errorSilently;
    private readonly AutoResetEvent _errorWaitHandle = new(false);
    private readonly string _ignoreErrorGateClose;
    private readonly string _ignoreErrorGateOpen;
    private readonly IStepLogger _logger;
    private readonly AutoResetEvent _outputWaitHandle = new(false);
    private readonly bool _raiseErrors;
    private readonly List<string> _results = new();
    private readonly bool _suppressOutput;
    private Exception _capturedException;
    private bool _ignoreErrors;
    private Process _process;

    public BuildProcessHelper(
        IStepLogger logger,
        CancellationTokenSource cancellationTokenSource,
        bool suppressOutput = false,
        bool raiseErrors = true,
        bool errorSilently = false,
        List<string> errorExclusions = null,
        string ignoreErrorGateClose = "",
        string ignoreErrorGateOpen = ""
    )
    {
        _logger = logger;
        _cancellationTokenSource = cancellationTokenSource;
        _suppressOutput = suppressOutput;
        _raiseErrors = raiseErrors;
        _errorSilently = errorSilently;
        _errorExclusions = errorExclusions;
        _ignoreErrorGateClose = ignoreErrorGateClose;
        _ignoreErrorGateOpen = ignoreErrorGateOpen;
    }

    public List<string> Run(string workingDirectory, string executable, string args, int timeout)
    {
        _process = new()
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

        using var unused = _cancellationTokenSource.Token.Register(_process.Kill);
        using var _ = _errorCancellationTokenSource.Token.Register(_process.Kill);

        _process.Start();
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        if (_process.WaitForExit(timeout) && _outputWaitHandle.WaitOne(timeout) && _errorWaitHandle.WaitOne(timeout))
        {
            if (_cancellationTokenSource.IsCancellationRequested)
            {
                return _results;
            }

            if (_capturedException != null)
            {
                if (_raiseErrors)
                {
                    throw _capturedException;
                }

                if (!_errorSilently)
                {
                    _logger.LogError(_capturedException);
                }
            }

            if (_raiseErrors && _process.ExitCode != 0)
            {
                var json = "";
                var messages = ExtractMessages(_results.Last(), ref json);
                if (messages.Any())
                {
                    throw new(messages.First().Item1);
                }

                throw new();
            }
        }
        else
        {
            // Timeout or cancelled
            if (!_cancellationTokenSource.IsCancellationRequested)
            {
                Exception exception = new($"Process exited with non-zero code ({_process.ExitCode})");
                if (_raiseErrors)
                {
                    throw exception;
                }

                if (!_errorSilently)
                {
                    _logger.LogError(exception);
                }
            }
        }

        return _results;
    }

    private void OnOutputDataReceived(object sender, DataReceivedEventArgs receivedEventArgs)
    {
        if (receivedEventArgs.Data == null)
        {
            _outputWaitHandle.Set();
            return;
        }

        var message = receivedEventArgs.Data;
        if (!string.IsNullOrEmpty(message))
        {
            _results.Add(message);
        }

        if (!_suppressOutput)
        {
            var json = "";
            try
            {
                var messages = ExtractMessages(message, ref json);
                foreach (var (text, colour) in messages)
                {
                    _logger.Log(text, colour);
                }
            }
            catch (Exception exception)
            {
                _capturedException = new($"Json failed: {json}\n\n{exception}");
                _errorCancellationTokenSource.Cancel();
            }
        }
    }

    private void OnErrorDataReceived(object sender, DataReceivedEventArgs receivedEventArgs)
    {
        if (receivedEventArgs.Data == null)
        {
            _errorWaitHandle.Set();
            return;
        }

        var message = receivedEventArgs.Data;
        if (string.IsNullOrEmpty(message) || CheckIgnoreErrorGates(message))
        {
            return;
        }

        if (_errorExclusions != null && _errorExclusions.Any(x => message.ContainsIgnoreCase(x)))
        {
            return;
        }

        _capturedException = new(message);
        _errorCancellationTokenSource.Cancel();
    }

    private bool CheckIgnoreErrorGates(string message)
    {
        if (message.ContainsIgnoreCase(_ignoreErrorGateClose))
        {
            _ignoreErrors = false;
            return true;
        }

        if (_ignoreErrors)
        {
            return true;
        }

        if (message.ContainsIgnoreCase(_ignoreErrorGateOpen))
        {
            _ignoreErrors = true;
            return true;
        }

        return false;
    }

    private static List<Tuple<string, string>> ExtractMessages(string message, ref string json)
    {
        List<Tuple<string, string>> messages = new();
        if (message.Length > 5 && message[..4] == "JSON")
        {
            var parts = message.Split('{', '}'); // covers cases where buffer gets extra data flushed to it after the closing brace
            json = $"{{{parts[1].Escape().Replace("\\\\n", "\\n")}}}";
            var jsonObject = JsonNode.Parse(json);
            messages.Add(new(jsonObject.GetValueFromObject("message"), jsonObject.GetValueFromObject("colour")));
            messages.AddRange(parts.Skip(2).Where(x => !string.IsNullOrEmpty(x)).Select(extra => new Tuple<string, string>(extra, "")));
        }
        else
        {
            messages.Add(new(message, ""));
        }

        return messages;
    }
}
