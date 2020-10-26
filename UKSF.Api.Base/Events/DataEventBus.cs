using System;
using System.Reactive.Linq;
using UKSF.Api.Base.Models;

namespace UKSF.Api.Base.Events {
    public interface IDataEventBus<T> : IEventBus<DataEventModel<T>> where T : DatabaseObject { }

    public class DataEventBus<T> : EventBus<DataEventModel<T>>, IDataEventBus<T> where T : DatabaseObject {
        public override IObservable<DataEventModel<T>> AsObservable() => Subject.OfType<DataEventModel<T>>();
    }
}
