using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UKSF.Api.Interfaces.Modpack;
using UKSF.Api.Interfaces.Modpack.BuildProcess;
using UKSF.Api.Models.Modpack;

namespace UKSF.Api.Services.Modpack.BuildProcess {
    public class BuildQueueService : IBuildQueueService {
        private readonly IBuildsService buildsService;
        private readonly IBuildProcessorService buildProcessorService;
        private readonly ConcurrentQueue<ModpackBuildQueueItem> queue = new ConcurrentQueue<ModpackBuildQueueItem>();
        private CancellationTokenSource currentCancellationTokenSource;
        private bool processing;

        public BuildQueueService(IBuildsService buildsService, IBuildProcessorService buildProcessorService) {
            this.buildsService = buildsService;
            this.buildProcessorService = buildProcessorService;
        }

        public void QueueBuild(string version, ModpackBuild build) {
            ModpackBuildRelease buildRelease = buildsService.GetBuildRelease(version);
            if (buildRelease == null) {
                throw new NullReferenceException($"Tried to add build to queue but could not find build release {version}");
            }

            queue.Enqueue(new ModpackBuildQueueItem {id = buildRelease.id, build = build});
            if (!processing) {
                // Processor not running, process as separate task
                Task unused = ProcessQueue();
            }
        }

        public void Cancel() {
            if (processing) {
                currentCancellationTokenSource.Cancel();
            }
        }

        private async Task ProcessQueue() {
            processing = true;
            while (queue.TryDequeue(out ModpackBuildQueueItem queueItem)) {
                currentCancellationTokenSource = new CancellationTokenSource();
                await buildProcessorService.ProcessBuild(queueItem.id, queueItem.build, currentCancellationTokenSource);
            }

            processing = false;
        }
    }
}
