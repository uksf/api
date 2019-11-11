using System.Collections.Concurrent;

namespace UKSFWebsite.Api.Services.Integrations {
    public static class PipeQueueManager {
        private static readonly ConcurrentQueue<string> PIPE_QUEUE = new ConcurrentQueue<string>();

        public static void QueueMessage(string message) {
            PIPE_QUEUE.Enqueue(message);
        }

        public static string GetMessage() {
            if (PIPE_QUEUE.Count > 0) {
                PIPE_QUEUE.TryDequeue(out string item);
                return item;
            }

            return string.Empty;
        }
    }
}
