using UKSFWebsite.Api.Models.Events.Types;
using UKSFWebsite.Api.Models.Message.Logging;

namespace UKSFWebsite.Api.Models.Events {
    public static class EventModelFactory {
        public static DataEventModel<TData> CreateDataEvent<TData>(DataEventType type, string id, object data = null) => new DataEventModel<TData> {type = type, id = id, data = data};
        public static SignalrEventModel CreateSignalrEvent(TeamspeakEventType procedure, object args) => new SignalrEventModel {procedure = procedure, args = args};
    }
}
