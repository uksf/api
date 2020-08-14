using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UKSF.Api.Interfaces.Game;
using UKSF.Api.Interfaces.Modpack.BuildProcess;
using UKSF.Api.Models.Modpack;
using UKSF.Api.Services.Message;

namespace UKSF.Api.Services.Modpack.BuildProcess {
    public class BuildQueueService : IBuildQueueService {
        private readonly IBuildProcessorService buildProcessorService;
        private readonly ConcurrentDictionary<string, Task> buildTasks = new ConcurrentDictionary<string, Task>();
        private readonly ConcurrentDictionary<string, CancellationTokenSource> cancellationTokenSources = new ConcurrentDictionary<string, CancellationTokenSource>();
        private readonly IGameServersService gameServersService;
        private ConcurrentQueue<ModpackBuild> queue = new ConcurrentQueue<ModpackBuild>();
        private bool processing;

        public BuildQueueService(IBuildProcessorService buildProcessorService, IGameServersService gameServersService) {
            this.buildProcessorService = buildProcessorService;
            this.gameServersService = gameServersService;
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
                                LogWrapper.Log($"Build {id} was cancelled but has not completed");
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
                    await Task.Delay(TimeSpan.FromMinutes(15));
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
