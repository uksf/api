using System;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using UKSF.Api.Base.Models;

namespace UKSF.Api.Shared.Extensions
{
    public static class ObservableExtensions
    {
        public static void SubscribeWithAsyncNext(this IObservable<EventModel> source, Func<EventModel, Task> onNext, Action<Exception> onError)
        {
            source.Select(x => Observable.Defer(() => onNext(x).ToObservable())).Concat().Subscribe(_ => { }, onError);
        }

        public static void SubscribeWithAsyncNext<T>(this IObservable<EventModel> source, Func<EventModel, T, Task> onNext, Action<Exception> onError)
        {
            source.Select(x => Observable.Defer(() => x.Data is T data ? onNext(x, data).ToObservable() : Task.CompletedTask.ToObservable())).Concat().Subscribe(_ => { }, onError);
        }
    }
}
