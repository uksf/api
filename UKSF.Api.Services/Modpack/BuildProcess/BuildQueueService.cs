﻿using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UKSF.Api.Interfaces.Game;
using UKSF.Api.Interfaces.Modpack.BuildProcess;
using UKSF.Api.Models.Modpack;

namespace UKSF.Api.Services.Modpack.BuildProcess {
    public class BuildQueueService : IBuildQueueService {
        private readonly IBuildProcessorService buildProcessorService;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> cancellationTokenSources = new ConcurrentDictionary<string, CancellationTokenSource>();
        private readonly IGameServersService gameServersService;
        private readonly ConcurrentQueue<ModpackBuild> queue = new ConcurrentQueue<ModpackBuild>();
        private bool processing;

        public BuildQueueService(IBuildProcessorService buildProcessorService, IGameServersService gameServersService) {
            this.buildProcessorService = buildProcessorService;
            this.gameServersService = gameServersService;
        }

        public void QueueBuild(ModpackBuild build) {
            queue.Enqueue(build);
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSources.TryAdd(build.id, cancellationTokenSource);
            if (!processing) {
                // Processor not running, process as separate task
                Task unused = ProcessQueue();
            }
        }

        public void Cancel(string id) {
            if (processing) {
                if (cancellationTokenSources.ContainsKey(id)) {
                    CancellationTokenSource cancellationTokenSource = cancellationTokenSources[id];
                    cancellationTokenSource.Cancel();
                }
            }
        }

        public void CancelAll() {
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
                    await Task.Delay(TimeSpan.FromSeconds(10)); // TODO: Increase delay
                    continue;
                }

                CancellationTokenSource cancellationTokenSource = cancellationTokenSources[build.id];
                await buildProcessorService.ProcessBuild(build, cancellationTokenSource);
            }

            processing = false;
        }
    }
}