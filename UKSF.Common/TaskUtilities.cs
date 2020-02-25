using System;
using System.Collections.Generic;
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

        public static async Task<IEnumerable<T>> WhenAll<T>(this IEnumerable<Task<T>> tasks) => await Task.WhenAll(tasks);
    }
}
