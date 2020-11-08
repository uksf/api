using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Base.Events;
using UKSF.Api.Modpack.Models;

namespace UKSF.Api.Modpack.Services.BuildProcess {
    public interface IBuildQueueService {
        void QueueBuild(ModpackBuild build);
        bool CancelQueued(string id);
        void Cancel(string id);
        void CancelAll();
    }

    public class BuildQueueService : IBuildQueueService {
        private readonly IBuildProcessorService buildProcessorService;
        private readonly ConcurrentDictionary<string, Task> buildTasks = new ConcurrentDictionary<string, Task>();
        private readonly ConcurrentDictionary<string, CancellationTokenSource> cancellationTokenSources = new ConcurrentDictionary<string, CancellationTokenSource>();
        private readonly IGameServersService gameServersService;
        private readonly ILogger logger;
        private ConcurrentQueue<ModpackBuild> queue = new ConcurrentQueue<ModpackBuild>();
        private bool processing;

        public BuildQueueService(IBuildProcessorService buildProcessorService, IGameServersService gameServersService, ILogger logger) {
            this.buildProcessorService = buildProcessorService;
            this.gameServersService = gameServersService;
            this.logger = logger;
        }

        public void QueueBuild(ModpackBuild build) {
            queue.Enqueue(build);
            if (!processing) {
                // Processor not running, process as separate task
                _ = ProcessQueue();
            }
        }

        public bool CancelQueued(string id) {
            if (queue.Any(x => x.id == id)) {
                queue = new ConcurrentQueue<ModpackBuild>(queue.Where(x => x.id != id));
                return true;
            }

            return false;
        }

        public void Cancel(string id) {
            if (cancellationTokenSources.ContainsKey(id)) {
                CancellationTokenSource cancellationTokenSource = cancellationTokenSources[id];
                cancellationTokenSource.Cancel();
                cancellationTokenSources.TryRemove(id, out CancellationTokenSource _);
            }

            if (buildTasks.ContainsKey(id)) {
                _ = Task.Run(
                    async () => {
                        await Task.Delay(TimeSpan.FromMinutes(1));
                        if (buildTasks.ContainsKey(id)) {
                            Task buildTask = buildTasks[id];

                            if (buildTask.IsCompleted) {
                                buildTasks.TryRemove(id, out Task _);
                            } else {
                                logger.LogWarning($"Build {id} was cancelled but has not completed within 1 minute of cancelling");
                            }
                        }
                    }
                );
            }
        }

        public void CancelAll() {
            queue.Clear();

            foreach ((string _, CancellationTokenSource cancellationTokenSource) in cancellationTokenSources) {
                cancellationTokenSource.Cancel();
            }

            cancellationTokenSources.Clear();
        }

        private async Task ProcessQueue() {
            processing = true;
            while (queue.TryDequeue(out ModpackBuild build)) {
                // TODO: Expand this to check if a server is running using the repo for this build. If no servers are running but there are processes, don't build at all.
                // Will require better game <-> api interaction to communicate with servers and headless clients properly
                if (gameServersService.GetGameInstanceCount() > 0) {
                    queue.Enqueue(build);
                    await Task.Delay(TimeSpan.FromMinutes(5));
                    continue;
                }

                CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
                cancellationTokenSources.TryAdd(build.id, cancellationTokenSource);
                Task buildTask = buildProcessorService.ProcessBuild(build, cancellationTokenSource);
                buildTasks.TryAdd(build.id, buildTask);
                await buildTask;
                cancellationTokenSources.TryRemove(build.id, out CancellationTokenSource _);
            }

            processing = false;
        }
    }
}
