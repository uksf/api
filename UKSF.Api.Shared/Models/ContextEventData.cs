namespace UKSF.Api.Shared.Models;

public class ContextEventData<T>
{
    public T Data { get; set; }
    public string Id { get; set; }

    public ContextEventData(string id, T data)
    {
        Id = id;
        Data = data;
    }
}
