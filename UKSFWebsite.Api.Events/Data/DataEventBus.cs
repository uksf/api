using System;
using System.Reactive.Linq;
using UKSFWebsite.Api.Interfaces.Events;
using UKSFWebsite.Api.Models.Events;

namespace UKSFWebsite.Api.Events.Data {
    public class DataEventBus<TData> : EventBus<DataEventModel<TData>>, IDataEventBus<TData> {
        public override IObservable<DataEventModel<TData>> AsObservable() => Subject.OfType<DataEventModel<TData>>();
    }
}
