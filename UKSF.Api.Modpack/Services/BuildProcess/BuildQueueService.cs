using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Shared.Events;

namespace UKSF.Api.Modpack.Services.BuildProcess {
    public interface IBuildQueueService {
        void QueueBuild(ModpackBuild build);
        bool CancelQueued(string id);
        void Cancel(string id);
        void CancelAll();
    }

    public class BuildQueueService : IBuildQueueService {
        private readonly IBuildProcessorService _buildProcessorService;
        private readonly ConcurrentDictionary<string, Task> _buildTasks = new ConcurrentDictionary<string, Task>();
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokenSources = new ConcurrentDictionary<string, CancellationTokenSource>();
        private readonly IGameServersService _gameServersService;
        private readonly ILogger _logger;
        private ConcurrentQueue<ModpackBuild> _queue = new ConcurrentQueue<ModpackBuild>();
        private bool _processing;

        public BuildQueueService(IBuildProcessorService buildProcessorService, IGameServersService gameServersService, ILogger logger) {
            _buildProcessorService = buildProcessorService;
            _gameServersService = gameServersService;
            _logger = logger;
        }

        public void QueueBuild(ModpackBuild build) {
            _queue.Enqueue(build);
            if (!_processing) {
                // Processor not running, process as separate task
                _ = ProcessQueue();
            }
        }

        public bool CancelQueued(string id) {
            if (_queue.Any(x => x.id == id)) {
                _queue = new ConcurrentQueue<ModpackBuild>(_queue.Where(x => x.id != id));
                return true;
            }

            return false;
        }

        public void Cancel(string id) {
            if (_cancellationTokenSources.ContainsKey(id)) {
                CancellationTokenSource cancellationTokenSource = _cancellationTokenSources[id];
                cancellationTokenSource.Cancel();
                _cancellationTokenSources.TryRemove(id, out CancellationTokenSource _);
            }

            if (_buildTasks.ContainsKey(id)) {
                _ = Task.Run(
                    async () => {
                        await Task.Delay(TimeSpan.FromMinutes(1));
                        if (_buildTasks.ContainsKey(id)) {
                            Task buildTask = _buildTasks[id];

                            if (buildTask.IsCompleted) {
                                _buildTasks.TryRemove(id, out Task _);
                            } else {
                                _logger.LogWarning($"Build {id} was cancelled but has not completed within 1 minute of cancelling");
                            }
                        }
                    }
                );
            }
        }

        public void CancelAll() {
            _queue.Clear();

            foreach ((string _, CancellationTokenSource cancellationTokenSource) in _cancellationTokenSources) {
                cancellationTokenSource.Cancel();
            }

            _cancellationTokenSources.Clear();
        }

        private async Task ProcessQueue() {
            _processing = true;
            while (_queue.TryDequeue(out ModpackBuild build)) {
                // TODO: Expand this to check if a server is running using the repo for this build. If no servers are running but there are processes, don't build at all.
                // Will require better game <-> api interaction to communicate with servers and headless clients properly
                if (_gameServersService.GetGameInstanceCount() > 0) {
                    _queue.Enqueue(build);
                    await Task.Delay(TimeSpan.FromMinutes(5));
                    continue;
                }

                CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
                _cancellationTokenSources.TryAdd(build.id, cancellationTokenSource);
                Task buildTask = _buildProcessorService.ProcessBuild(build, cancellationTokenSource);
                _buildTasks.TryAdd(build.id, buildTask);
                await buildTask;
                _cancellationTokenSources.TryRemove(build.id, out CancellationTokenSource _);
            }

            _processing = false;
        }
    }
}
