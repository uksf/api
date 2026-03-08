using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.Services;

public interface IRptLogService
{
    List<RptLogSource> GetLogSources(DomainGameServer server);
    string GetLatestRptFilePath(DomainGameServer server, string source);
    (List<string> Lines, long BytesRead) ReadFullFile(string filePath);
    IDisposable WatchFile(string filePath, long startOffset, Func<List<string>, Task> onNewContent);
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

    public IDisposable WatchFile(string filePath, long startOffset, Func<List<string>, Task> onNewContent)
    {
        return new FileLogWatcher(filePath, startOffset, onNewContent);
    }

    private sealed class FileLogWatcher : IDisposable
    {
        private readonly FileSystemWatcher _fileSystemWatcher;
        private readonly string _filePath;
        private readonly Func<List<string>, Task> _onNewContent;
        private long _offset;
        private int _reading;
        private bool _pendingRead;
        private bool _disposed;

        public FileLogWatcher(string filePath, long startOffset, Func<List<string>, Task> onNewContent)
        {
            _filePath = filePath;
            _onNewContent = onNewContent;
            _offset = startOffset;

            _fileSystemWatcher = new FileSystemWatcher(Path.GetDirectoryName(filePath)!, Path.GetFileName(filePath))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size, EnableRaisingEvents = true
            };
            _fileSystemWatcher.Changed += OnFileChanged;
        }

        private async void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            if (_disposed)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref _reading, 1, 0) != 0)
            {
                _pendingRead = true;
                return;
            }

            try
            {
                do
                {
                    _pendingRead = false;
                    await ReadNewContentAsync();
                }
                while (_pendingRead && !_disposed);
            }
            catch
            {
                // File access errors are transient — next Changed event will retry
            }
            finally
            {
                Interlocked.Exchange(ref _reading, 0);
            }
        }

        private async Task ReadNewContentAsync()
        {
            var fileLength = new FileInfo(_filePath).Length;
            if (fileLength < _offset)
            {
                _offset = 0;
            }

            if (fileLength <= _offset)
            {
                return;
            }

            using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            stream.Seek(_offset, SeekOrigin.Begin);
            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync();
            _offset = stream.Position;

            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToList();
            if (lines.Count > 0)
            {
                await _onNewContent(lines);
            }
        }

        public void Dispose()
        {
            _disposed = true;
            _fileSystemWatcher.EnableRaisingEvents = false;
            _fileSystemWatcher.Dispose();
        }
    }
}
