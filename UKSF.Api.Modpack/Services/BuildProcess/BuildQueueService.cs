using System.Collections.Concurrent;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using UKSF.Api.Modpack.Models;

namespace UKSF.Api.Modpack.Services.BuildProcess;

public interface IBuildQueueService
{
    void QueueBuild(ModpackBuild build);
    bool CancelQueued(string id);
    void Cancel(string id);
    void CancelAll();
}

public class BuildQueueService : IBuildQueueService
{
    private readonly IBuildProcessorService _buildProcessorService;
    private readonly ConcurrentDictionary<string, Task> _buildTasks = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokenSources = new();
    private readonly IGameServersService _gameServersService;
    private readonly IUksfLogger _logger;
    private bool _processing;
    private ConcurrentQueue<ModpackBuild> _queue = new();

    public BuildQueueService(IBuildProcessorService buildProcessorService, IGameServersService gameServersService, IUksfLogger logger)
    {
        _buildProcessorService = buildProcessorService;
        _gameServersService = gameServersService;
        _logger = logger;
    }

    public void QueueBuild(ModpackBuild build)
    {
        _queue.Enqueue(build);
        if (!_processing)
        {
            // Processor not running, process as separate task
            _ = ProcessQueue();
        }
    }

    public bool CancelQueued(string id)
    {
        if (_queue.Any(x => x.Id == id))
        {
            _queue = new ConcurrentQueue<ModpackBuild>(_queue.Where(x => x.Id != id));
            return true;
        }

        return false;
    }

    public void Cancel(string id)
    {
        if (_cancellationTokenSources.TryGetValue(id, out var source))
        {
            source.Cancel();
            _cancellationTokenSources.TryRemove(id, out _);
        }

        if (_buildTasks.ContainsKey(id))
        {
            _ = Task.Run(
                async () =>
                {
                    await Task.Delay(TimeSpan.FromMinutes(1));
                    if (_buildTasks.TryGetValue(id, out var task))
                    {
                        if (task.IsCompleted)
                        {
                            _buildTasks.TryRemove(id, out _);
                        }
                        else
                        {
                            _logger.LogWarning($"Build {id} was cancelled but has not completed within 1 minute of cancelling");
                        }
                    }
                }
            );
        }
    }

    public void CancelAll()
    {
        _queue.Clear();

        foreach (var (_, cancellationTokenSource) in _cancellationTokenSources)
        {
            cancellationTokenSource.Cancel();
        }

        _cancellationTokenSources.Clear();
    }

    private async Task ProcessQueue()
    {
        _processing = true;
        while (_queue.TryDequeue(out var build))
        {
            // TODO: Expand this to check if a server is running using the repo for this build. If no servers are running but there are processes, don't build at all.
            // Will require better game <-> api interaction to communicate with servers and headless clients properly
            // if (_gameServersService.GetGameInstanceCount() > 0) {
            //     _queue.Enqueue(build);
            //     await Task.Delay(TimeSpan.FromMinutes(5));
            //     continue;
            // }

            CancellationTokenSource cancellationTokenSource = new();
            _cancellationTokenSources.TryAdd(build.Id, cancellationTokenSource);

            var buildTask = _buildProcessorService.ProcessBuildWithErrorHandling(build, cancellationTokenSource);
            _buildTasks.TryAdd(build.Id, buildTask);

            await buildTask;

            _cancellationTokenSources.TryRemove(build.Id, out _);
        }

        _processing = false;
    }
}
