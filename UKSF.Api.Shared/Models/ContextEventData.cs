namespace UKSF.Api.Shared.Models {
    public class ContextEventData<T> {
        public ContextEventData(string id, T data) {
            Id = id;
            Data = data;
        }

        public string Id { get; }
        public T Data { get; }
    }
}
