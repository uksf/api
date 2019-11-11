using UKSFWebsite.Api.Models.Events;

namespace UKSFWebsite.Api.Events.Data {
    public static class DataEventFactory {
        public static DataEventModel Create(DataEventType type, string id, object data = null) => new DataEventModel {type = type, id = id, data = data};
    }
}
