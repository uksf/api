using UKSF.Api.Models.Events;

namespace UKSF.Api.Interfaces.Events {
    public interface IDataEventBus<TData> : IEventBus<DataEventModel<TData>> { }
}
