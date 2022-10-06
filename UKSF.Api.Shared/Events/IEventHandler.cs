namespace UKSF.Api.Shared.Events;

public interface IEventHandler
{
    void EarlyInit();
    void Init();
}
