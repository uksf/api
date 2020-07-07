using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UKSF.Api.Interfaces.Modpack.BuildProcess;
using UKSF.Api.Models.Modpack;

namespace UKSF.Api.Services.Modpack.BuildProcess {
    public class BuildQueueService : IBuildQueueService {
        private readonly IBuildProcessorService buildProcessorService;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> cancellationTokenSources = new ConcurrentDictionary<string, CancellationTokenSource>();
        private readonly ConcurrentQueue<ModpackBuild> queue = new ConcurrentQueue<ModpackBuild>();
        private bool processing;

        public BuildQueueService(IBuildProcessorService buildProcessorService) => this.buildProcessorService = buildProcessorService;

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
                CancellationTokenSource cancellationTokenSource = cancellationTokenSources[build.id];
                await buildProcessorService.ProcessBuild(build, cancellationTokenSource);
            }

            processing = false;
        }
    }
}
