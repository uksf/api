namespace UKSF.Api.Base.Events;

public interface IEventHandler
{
    void EarlyInit();
    void Init();
}
