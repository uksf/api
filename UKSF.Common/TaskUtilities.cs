using System;
using System.Threading;
using System.Threading.Tasks;

namespace UKSF.Common {
    public static class TaskUtilities {
        public static async Task Delay(TimeSpan timeSpan, CancellationToken token) {
            try {
                await Task.Delay(timeSpan, token);
            } catch (Exception) {
                // Ignored
            }
        }
    }
}
