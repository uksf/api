using System;
using System.Reactive.Linq;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Models;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Shared.Events {
    public interface IDataEventBus<T> : IEventBus<DataEventModel<T>> where T : DatabaseObject { }

    public class DataEventBus<T> : EventBus<DataEventModel<T>>, IDataEventBus<T> where T : DatabaseObject {
        public override IObservable<DataEventModel<T>> AsObservable() => Subject.OfType<DataEventModel<T>>();
    }
}
