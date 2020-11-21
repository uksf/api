using UKSF.Api.Base.Models;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Shared.Events {
    public static class EventModelFactory {
        public static DataEventModel<T> CreateDataEvent<T>(DataEventType type, string id, object data = null) where T : MongoObject => new() { Type = type, Id = id, Data = data };
    }
}
