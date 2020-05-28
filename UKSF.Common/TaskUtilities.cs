using System;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
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

        public static void SubscribeAsync<T>(this IObservable<T> source, Func<T, Task> onNext, Action<Exception> onError) {
            source.Select(x => Observable.Defer(() => onNext(x).ToObservable())).Concat().Subscribe(x => { }, onError);
        }
    }
}
