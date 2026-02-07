using System.Reactive.Linq;
using UKSF.Api.Core.Models;

namespace UKSF.Api.Core.Extensions;

public static class ObservableExtensions
{
    extension(IObservable<EventModel> source)
    {
        public IDisposable SubscribeWithAsyncNext<T>(Func<EventModel, T, Task> onNext, Action<Exception> onError) where T : EventData
        {
            return source.Select(x => Observable.FromAsync(() => x.Data is T data ? onNext(x, data) : Task.CompletedTask))
                         .Concat()
                         .Subscribe(_ => { }, onError);
        }
    }
}
