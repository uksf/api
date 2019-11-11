namespace UKSFWebsite.Api.Models.Events {
    public enum DataEventType {
        ADD,
        UPDATE,
        DELETE
    }
    
    public class DataEventModel {
        public DataEventType type;
        public string id;
        public object data;
    }
}
