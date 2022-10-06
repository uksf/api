namespace UKSF.Api.Signalr.Clients;

public interface IUtilityClient
{
    Task ReceiveFrontendUpdate(string version);
}
