using System;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;

namespace UKSF.Api.Base.Extensions {
    public static class ObservableExtensions {
        public static void SubscribeWithAsyncNext<T>(this IObservable<T> source, Func<T, Task> onNext, Action<Exception> onError) {
            source.Select(x => Observable.Defer(() => onNext(x).ToObservable())).Concat().Subscribe(x => { }, onError);
        }
    }
}
