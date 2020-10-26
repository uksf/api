using UKSF.Api.Base.Models;

namespace UKSF.Api.Base.Events {
    public static class EventModelFactory {
        public static DataEventModel<T> CreateDataEvent<T>(DataEventType type, string id, object data = null) where T : DatabaseObject => new DataEventModel<T> { type = type, id = id, data = data };
    }
}
