namespace UKSF.Api.Shared.Models
{
    public class ContextEventData<T>
    {
        public T Data;
        public string Id;

        public ContextEventData(string id, T data)
        {
            Id = id;
            Data = data;
        }
    }
}
