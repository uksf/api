using UKSF.Api.Models;
using UKSF.Api.Models.Events;

namespace UKSF.Api.Data {
    public static class EventModelFactory {
        public static DataEventModel<T> CreateDataEvent<T>(DataEventType type, string id, object data = null) where T : DatabaseObject => new DataEventModel<T> { type = type, id = id, data = data };
    }
}
