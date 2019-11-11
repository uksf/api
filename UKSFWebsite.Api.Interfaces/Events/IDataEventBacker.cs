using System;
using UKSFWebsite.Api.Models.Events;

namespace UKSFWebsite.Api.Interfaces.Events {
    public interface IDataEventBacker {
        IObservable<DataEventModel> EventBus();
    }
}
