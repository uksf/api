using UKSFWebsite.Api.Models.Events;

namespace UKSFWebsite.Api.Events.Data {
    public static class EventModelFactory {
        public static DataEventModel CreateDataEvent(DataEventType type, string id, object data = null) => new DataEventModel {type = type, id = id, data = data};
        public static SocketEventModel CreateSocketEvent(string clientName, string message) => new SocketEventModel {clientName = clientName, message = message};
    }
}
