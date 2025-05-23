using System.Collections.Concurrent;
using System.Threading.Channels;
using UKSF.Api.Core;
using UKSF.Api.Modpack.Models;

namespace UKSF.Api.Modpack.BuildProcess;

public interface IBuildQueueService
{
    void QueueBuild(DomainModpackBuild build);
    bool CancelQueued(string buildId);
    void CancelRunning(string buildId);
    void CancelAll();
}

public class BuildQueueService : IBuildQueueService
{
    private readonly IBuildProcessorService _buildProcessorService;
    private readonly IUksfLogger _logger;
    private readonly int _cleanupDelaySeconds;
    private readonly ConcurrentDictionary<string, Task> _buildTasks = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokenSources = new();

    private readonly Channel<DomainModpackBuild> _channel =
        Channel.CreateUnbounded<DomainModpackBuild>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    private readonly ConcurrentDictionary<string, DomainModpackBuild> _queuedBuilds = new();

    public BuildQueueService(IBuildProcessorService buildProcessorService, IUksfLogger logger, int cleanupDelaySeconds = 60)
    {
        _buildProcessorService = buildProcessorService;
        _logger = logger;
        _cleanupDelaySeconds = cleanupDelaySeconds;

        _ = ProcessQueueAsync();
    }

    public void QueueBuild(DomainModpackBuild build)
    {
        _queuedBuilds.TryAdd(build.Id, build);
        _channel.Writer.TryWrite(build);
    }

    public bool CancelQueued(string buildId)
    {
        return _queuedBuilds.TryRemove(buildId, out _);
    }

    public void CancelRunning(string buildId)
    {
        if (_cancellationTokenSources.TryGetValue(buildId, out var source))
        {
            source.Cancel();
            _cancellationTokenSources.TryRemove(buildId, out _);
        }

        if (_buildTasks.ContainsKey(buildId))
        {
            _ = Task.Run(
                async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(_cleanupDelaySeconds));
                    if (_buildTasks.TryGetValue(buildId, out var task))
                    {
                        if (task.IsCompleted)
                        {
                            _buildTasks.TryRemove(buildId, out _);
                        }
                        else
                        {
                            _logger.LogWarning($"Build {buildId} was cancelled but has not completed within {_cleanupDelaySeconds} seconds of cancelling");
                        }
                    }
                }
            );
        }
    }

    public void CancelAll()
    {
        _queuedBuilds.Clear();

        foreach (var (_, cancellationTokenSource) in _cancellationTokenSources)
        {
            cancellationTokenSource.Cancel();
        }

        _cancellationTokenSources.Clear();
    }

    private async Task ProcessQueueAsync()
    {
        await foreach (var build in _channel.Reader.ReadAllAsync())
        {
            _queuedBuilds.TryRemove(build.Id, out _);

            CancellationTokenSource cancellationTokenSource = new();
            _cancellationTokenSources.TryAdd(build.Id, cancellationTokenSource);

            var buildTask = _buildProcessorService.ProcessBuildWithErrorHandling(build, cancellationTokenSource);
            _buildTasks.TryAdd(build.Id, buildTask);

            await buildTask;

            _cancellationTokenSources.TryRemove(build.Id, out _);
        }
    }
}
