namespace UKSF.Api.Base.Models {
    public enum DataEventType {
        ADD,
        UPDATE,
        DELETE
    }

    // ReSharper disable once UnusedTypeParameter
    public class DataEventModel<T> where T : DatabaseObject {
        public object data;
        public string id;
        public DataEventType type;
    }
}
