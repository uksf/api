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
    Task CancelAll();
}

public class BuildQueueService : IBuildQueueService
{
    private readonly IBuildProcessorService _buildProcessorService;
    private readonly ConcurrentDictionary<string, Task> _buildTasks = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokenSources = new();

    private readonly Channel<DomainModpackBuild> _channel =
        Channel.CreateUnbounded<DomainModpackBuild>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    private readonly int _cleanupDelaySeconds;
    private readonly IUksfLogger _logger;

    private readonly ConcurrentDictionary<string, DomainModpackBuild> _queuedBuilds = new();

    public BuildQueueService(IBuildProcessorService buildProcessorService, IUksfLogger logger, int cleanupDelaySeconds = 30)
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
        _logger.LogInfo($"Cancelling running build {buildId}");

        if (_cancellationTokenSources.TryGetValue(buildId, out var source))
        {
            source.Cancel();
            _cancellationTokenSources.TryRemove(buildId, out _);
            _logger.LogInfo($"Sent cancellation signal to build {buildId}");
        }
        else
        {
            _logger.LogWarning($"No cancellation token found for build {buildId}");
        }

        if (_buildTasks.ContainsKey(buildId))
        {
            _ = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(_cleanupDelaySeconds));

                    if (_buildTasks.TryGetValue(buildId, out var task))
                    {
                        if (task.IsCompleted)
                        {
                            _buildTasks.TryRemove(buildId, out _);
                            _logger.LogInfo($"Build {buildId} completed and cleaned up successfully");
                        }
                        else
                        {
                            _logger.LogWarning(
                                $"Build {buildId} was cancelled but has not completed within {_cleanupDelaySeconds} seconds of cancelling. Task status: {task.Status}"
                            );

                            // More aggressive cleanup - force remove the task
                            _buildTasks.TryRemove(buildId, out _);
                            _logger.LogWarning($"Forcibly removed build task {buildId} from tracking");

                            // Log additional debug information
                            _logger.LogInfo($"Current build tasks count: {_buildTasks.Count}, Cancellation tokens count: {_cancellationTokenSources.Count}");
                        }
                    }
                }
            );
        }
        else
        {
            _logger.LogInfo($"No build task found for build {buildId} (may have already completed)");
        }
    }

    public async Task CancelAll()
    {
        _logger.LogInfo($"Cancelling {_queuedBuilds.Count} builds");
        _queuedBuilds.Clear();

        _logger.LogInfo($"Cancelling {_cancellationTokenSources.Count} build process tokens");
        var cancelTasks = _cancellationTokenSources.Select(cancellationTokenSource => cancellationTokenSource.Value.CancelAsync());
        await Task.WhenAll(cancelTasks);

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
