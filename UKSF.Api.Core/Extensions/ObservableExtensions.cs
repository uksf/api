using System.Reactive.Linq;
using UKSF.Api.Core.Models;

namespace UKSF.Api.Core.Extensions;

public static class ObservableExtensions
{
    public static void SubscribeWithAsyncNext<T>(this IObservable<EventModel> source, Func<EventModel, T, Task> onNext, Action<Exception> onError)
        where T : EventData
    {
        source.Select(x => Observable.FromAsync(() => x.Data is T data ? onNext(x, data) : Task.CompletedTask)).Concat().Subscribe(_ => { }, onError);
    }
}
