using System;
using UKSFWebsite.Api.Interfaces.Events;
using UKSFWebsite.Api.Models.Events;

namespace UKSFWebsite.Api.Events.Data {
    public abstract class DataEventBacker<TData> : IDataEventBacker<TData> {
        private readonly IDataEventBus<TData> dataEventBus;

        protected DataEventBacker(IDataEventBus<TData> dataEventBus) => this.dataEventBus = dataEventBus;

        public IObservable<DataEventModel<TData>> EventBus() => dataEventBus.AsObservable();

        protected virtual void DataEvent(DataEventModel<TData> dataEvent) {
            dataEventBus.Send(dataEvent);
        }
    }
}
