namespace UKSF.Api.Core.Models;

public class ContextEventData<T>
{
    public ContextEventData(string id, T data)
    {
        Id = id;
        Data = data;
    }

    public T Data { get; set; }
    public string Id { get; set; }
}
