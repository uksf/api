using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.Services;

public interface IRptLogService
{
    List<RptLogSource> GetLogSources(DomainGameServer server);
    string GetLatestRptFilePath(DomainGameServer server, string source);
    (List<string> Lines, long BytesRead) ReadFullFile(string filePath);
    RptLogSearchResponse SearchFile(string filePath, string query);
    IDisposable WatchFile(string filePath, long startOffset, Action<List<string>> onNewContent);
}

public class RptLogService(IVariablesService variablesService) : IRptLogService
{
    public List<RptLogSource> GetLogSources(DomainGameServer server)
    {
        var sources = new List<RptLogSource> { new("Server", true) };

        if (server.NumberHeadlessClients > 0)
        {
            var hcNames = variablesService.GetVariable("SERVER_HEADLESS_NAMES").AsArray();
            for (var i = 0; i < server.NumberHeadlessClients; i++)
            {
                sources.Add(new RptLogSource(hcNames[i], false));
            }
        }

        return sources;
    }

    public string GetLatestRptFilePath(DomainGameServer server, string source)
    {
        var validSources = GetLogSources(server).Select(s => s.Name).ToHashSet();
        if (!validSources.Contains(source))
        {
            return null;
        }

        var profilesPath = variablesService.GetVariable("SERVER_PATH_PROFILES").AsString();
        var profileDir = source == "Server" ? Path.Combine(profilesPath, server.Name) : Path.Combine(profilesPath, $"{server.Name}{source}");

        if (!Directory.Exists(profileDir))
        {
            return null;
        }

        return Directory.GetFiles(profileDir, "*.rpt").OrderByDescending(f => new FileInfo(f).LastWriteTime).FirstOrDefault();
    }

    public (List<string> Lines, long BytesRead) ReadFullFile(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        var lines = new List<string>();
        while (reader.ReadLine() is { } line)
        {
            lines.Add(line);
        }

        return (lines, stream.Position);
    }

    public RptLogSearchResponse SearchFile(string filePath, string query)
    {
        var (lines, _) = ReadFullFile(filePath);
        var results = new List<RptLogSearchResult>();
        var totalMatches = 0;

        for (var i = 0; i < lines.Count; i++)
        {
            var count = 0;
            var index = 0;
            while ((index = lines[i].IndexOf(query, index, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                count++;
                index += query.Length;
            }

            if (count > 0)
            {
                results.Add(new RptLogSearchResult(i, lines[i]));
                totalMatches += count;
            }
        }

        return new RptLogSearchResponse(results, totalMatches);
    }

    public IDisposable WatchFile(string filePath, long startOffset, Action<List<string>> onNewContent)
    {
        return new FileLogWatcher(filePath, startOffset, onNewContent);
    }

    private sealed class FileLogWatcher : IDisposable
    {
        private readonly Action<List<string>> _onNewContent;
        private readonly FileSystemWatcher _fileSystemWatcher;
        private readonly CancellationTokenSource _cts = new();
        private readonly object _lock = new();
        private long _offset;
        private bool _disposed;

        public FileLogWatcher(string filePath, long startOffset, Action<List<string>> onNewContent)
        {
            _onNewContent = onNewContent;
            _offset = startOffset;

            var directory = Path.GetDirectoryName(filePath)!;
            var fileName = Path.GetFileName(filePath);

            _fileSystemWatcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size, EnableRaisingEvents = true
            };
            _fileSystemWatcher.Changed += (_, _) => ReadNewContent(filePath);

            _ = RunTimerFallbackAsync(filePath);
        }

        private async Task RunTimerFallbackAsync(string filePath)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
            try
            {
                while (await timer.WaitForNextTickAsync(_cts.Token))
                {
                    ReadNewContent(filePath);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on dispose
            }
        }

        private void ReadNewContent(string filePath)
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length < _offset)
                {
                    _offset = 0;
                }

                if (fileInfo.Length <= _offset)
                {
                    return;
                }

                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                stream.Seek(_offset, SeekOrigin.Begin);

                using var reader = new StreamReader(stream);
                var content = reader.ReadToEnd();
                _offset = stream.Position;

                var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToList();

                if (lines.Count > 0)
                {
                    _onNewContent(lines);
                }
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
            }

            _fileSystemWatcher.EnableRaisingEvents = false;
            _fileSystemWatcher.Dispose();
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
