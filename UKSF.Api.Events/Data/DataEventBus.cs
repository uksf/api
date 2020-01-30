using System;
using System.Reactive.Linq;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Events;

namespace UKSF.Api.Events.Data {
    public class DataEventBus<TData> : EventBus<DataEventModel<TData>>, IDataEventBus<TData> {
        public override IObservable<DataEventModel<TData>> AsObservable() => Subject.OfType<DataEventModel<TData>>();
    }
}
