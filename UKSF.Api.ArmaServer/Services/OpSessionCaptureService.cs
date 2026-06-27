using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.ArmaServer.Services;

public interface IOpSessionCaptureService
{
    Task CaptureStartedAsync(string serverId, string sessionId);
    Task CaptureEndedAsync(string sessionId);
}

public class OpSessionCaptureService(IOpsContext opsContext) : IOpSessionCaptureService
{
    public async Task CaptureStartedAsync(string serverId, string sessionId)
    {
        if (string.IsNullOrEmpty(serverId) || string.IsNullOrEmpty(sessionId))
        {
            return;
        }

        var pending = opsContext
                      .Get(x => x.LaunchedServerId == serverId && x.Status == OpStatus.Scheduled && string.IsNullOrEmpty(x.SessionId))
                      .OrderByDescending(x => x.LaunchedAt)
                      .FirstOrDefault();
        if (pending is null)
        {
            return;
        }

        await opsContext.Update(pending.Id, x => x.SessionId, sessionId);
    }

    public async Task CaptureEndedAsync(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            return;
        }

        var op = opsContext.Get(x => x.SessionId == sessionId).FirstOrDefault();
        if (op is null)
        {
            return;
        }

        await opsContext.Update(op.Id, x => x.Status, OpStatus.Complete);
    }
}
