using System;
using UKSF.Api.Models.Events;

namespace UKSF.Api.Interfaces.Events {
    public interface IDataEventBacker<TData> {
        IObservable<DataEventModel<TData>> EventBus();
    }
}
