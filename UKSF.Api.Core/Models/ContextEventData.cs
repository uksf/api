namespace UKSF.Api.Core.Models;

public class ContextEventData<T>(string id, T data) : EventData
{
    public T Data { get; } = data;
    public string Id { get; } = id;
}
