namespace UKSF.Api.Models.Events {
    public enum DataEventType {
        ADD,
        UPDATE,
        DELETE
    }

    // ReSharper disable once UnusedTypeParameter
    public class DataEventModel<TData> {
        public object data;
        public string id;
        public DataEventType type;
    }
}