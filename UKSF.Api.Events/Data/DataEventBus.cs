using System;
using System.Reactive.Linq;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models;
using UKSF.Api.Models.Events;

namespace UKSF.Api.Events.Data {
    public class DataEventBus<T> : EventBus<DataEventModel<T>>, IDataEventBus<T> where T : DatabaseObject {
        public override IObservable<DataEventModel<T>> AsObservable() => Subject.OfType<DataEventModel<T>>();
    }
}
