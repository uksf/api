using Microsoft.AspNetCore.SignalR;

namespace UKSF.Api.Core.Signalr;

public class HubExceptionFilter(IUksfLogger logger) : IHubFilter
{
    public async ValueTask<object> InvokeMethodAsync(HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object>> next)
    {
        try
        {
            return await next(invocationContext);
        }
        catch (HubException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var hubName = invocationContext.Hub.GetType().Name;
            var methodName = invocationContext.HubMethodName;
            logger.LogError($"Unhandled exception in {hubName}.{methodName}", ex);
            throw new HubException($"{hubName}.{methodName} failed: {ex.Message}");
        }
    }
}
