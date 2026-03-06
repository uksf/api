using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.ArmaServer.Signalr.Clients;
using UKSF.Api.Core;

namespace UKSF.Api.ArmaServer.Signalr.Hubs;

[Authorize]
public class ServersHub(
    IRptLogService rptLogService,
    IGameServersContext gameServersContext,
    IHubContext<ServersHub, IServersClient> hubContext,
    ILogSubscriptionService logSubscriptionService,
    IUksfLogger logger
) : Hub<IServersClient>
{
    public const string EndPoint = "servers";

    public async Task SubscribeToLog(string serverId, string source)
    {
        var server = gameServersContext.GetSingle(serverId);
        var filePath = rptLogService.GetLatestRptFilePath(server, source);

        if (filePath == null)
        {
            await Clients.Caller.ReceiveLogContent(serverId, source, [], 0, true);
            return;
        }

        var groupName = $"log:{serverId}:{source}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        var isNew = logSubscriptionService.AddSubscription(Context.ConnectionId, groupName);
        if (!isNew)
        {
            return;
        }

        List<string> lines;
        long bytesRead;
        try
        {
            (lines, bytesRead) = rptLogService.ReadFullFile(filePath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex);
            await Clients.Caller.ReceiveLogContent(serverId, source, [], 0, true);
            return;
        }

        const int chunkSize = 1000;
        for (var i = 0; i < lines.Count; i += chunkSize)
        {
            var chunk = lines.GetRange(i, Math.Min(chunkSize, lines.Count - i));
            var isComplete = i + chunkSize >= lines.Count;
            await Clients.Caller.ReceiveLogContent(serverId, source, chunk, i, isComplete);
        }

        if (lines.Count == 0)
        {
            await Clients.Caller.ReceiveLogContent(serverId, source, [], 0, true);
        }

        logSubscriptionService.StartOrJoinWatcher(
            groupName,
            () => rptLogService.WatchFile(
                filePath,
                bytesRead,
                newLines =>
                {
                    try
                    {
                        hubContext.Clients.Group(groupName).ReceiveLogAppend(serverId, source, newLines);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex);
                    }
                }
            )
        );
    }

    public async Task UnsubscribeFromLog(string serverId, string source)
    {
        var groupName = $"log:{serverId}:{source}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        logSubscriptionService.RemoveSubscription(Context.ConnectionId, groupName);
        logSubscriptionService.StopOrLeaveWatcher(groupName);
    }

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        var subs = logSubscriptionService.RemoveAllSubscriptions(Context.ConnectionId);
        foreach (var groupName in subs)
        {
            logSubscriptionService.StopOrLeaveWatcher(groupName);
        }

        await base.OnDisconnectedAsync(exception);
    }
}
