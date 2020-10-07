using UKSF.Api.Models;
using UKSF.Api.Models.Events;

namespace UKSF.Api.Interfaces.Events {
    public interface IDataEventBus<T> : IEventBus<DataEventModel<T>> where T : DatabaseObject { }
}
