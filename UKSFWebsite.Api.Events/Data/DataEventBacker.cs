using System;
using UKSFWebsite.Api.Interfaces.Events;
using UKSFWebsite.Api.Models.Events;

namespace UKSFWebsite.Api.Events.Data {
    public abstract class DataEventBacker : IDataEventBacker {
        private readonly IDataEventBus dataEventBus;

        protected DataEventBacker(IDataEventBus dataEventBus) => this.dataEventBus = dataEventBus;

        public IObservable<DataEventModel> EventBus() => dataEventBus.AsObservable();

        protected virtual void DataEvent(DataEventModel dataEvent) {
            dataEventBus.Send(dataEvent);
        }
    }
}
