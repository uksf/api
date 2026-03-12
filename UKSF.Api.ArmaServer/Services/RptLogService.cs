using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.Services;

public interface IRptLogService
{
    List<RptLogSource> GetLogSources(DomainGameServer server);
    string GetLatestRptFilePath(DomainGameServer server, string source);
    Task<long> ReadChunksAsync(string filePath, int chunkSize, Func<List<string>, bool, Task> onChunk, CancellationToken cancellationToken = default);
    IDisposable WatchFile(string filePath, long startOffset, Func<List<string>, Task> onNewContent);
}

public class RptLogService(IVariablesService variablesService, IUksfLogger logger) : IRptLogService
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

    public async Task<long> ReadChunksAsync(
        string filePath,
        int chunkSize,
        Func<List<string>, bool, Task> onChunk,
        CancellationToken cancellationToken = default
    )
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        var chunk = new List<string>(chunkSize);

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            chunk.Add(line);
            if (chunk.Count >= chunkSize)
            {
                await onChunk(chunk, false);
                chunk = new List<string>(chunkSize);
            }
        }

        await onChunk(chunk, true);
        return stream.Position;
    }

    public IDisposable WatchFile(string filePath, long startOffset, Func<List<string>, Task> onNewContent)
    {
        return new FilePollingWatcher(filePath, startOffset, onNewContent, logger);
    }

    private sealed class FilePollingWatcher : IDisposable
    {
        private readonly CancellationTokenSource _cts = new();

        public FilePollingWatcher(string filePath, long startOffset, Func<List<string>, Task> onNewContent, IUksfLogger logger)
        {
            _ = PollAsync(filePath, startOffset, onNewContent, logger, _cts.Token);
        }

        private static async Task PollAsync(string filePath, long offset, Func<List<string>, Task> onNewContent, IUksfLogger logger, CancellationToken ct)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(1000));
            try
            {
                while (await timer.WaitForNextTickAsync(ct))
                {
                    try
                    {
                        var length = new FileInfo(filePath).Length;
                        if (length < offset) offset = 0;
                        if (length <= offset) continue;

                        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        stream.Seek(offset, SeekOrigin.Begin);
                        using var reader = new StreamReader(stream);
                        var lines = new List<string>();
                        while (await reader.ReadLineAsync(ct) is { } line)
                        {
                            lines.Add(line);
                        }

                        offset = stream.Position;
                        if (lines.Count > 0) await onNewContent(lines);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning($"Error polling RPT file '{filePath}': {ex.Message}");
                    }
                }
            }
            catch (OperationCanceledException) { }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
