namespace UKSFWebsite.Api.Models.Events {
    public enum DataEventType {
        ADD,
        UPDATE,
        DELETE
    }
    
    // ReSharper disable once UnusedTypeParameter
    public class DataEventModel<TData> {
        public DataEventType type;
        public string id;
        public object data;
    }
}
