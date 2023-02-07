namespace UKSF.Api.Core.Events;

public interface IEventHandler
{
    void EarlyInit();
    void Init();
}
