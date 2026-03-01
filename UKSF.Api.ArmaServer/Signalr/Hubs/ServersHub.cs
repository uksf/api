using System.Collections.Concurrent;
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
    IUksfLogger logger
) : Hub<IServersClient>
{
    public const string EndPoint = "servers";

    private static readonly ConcurrentDictionary<string, HashSet<string>> ConnectionSubscriptions = new();
    private static readonly ConcurrentDictionary<string, (IDisposable Watcher, int RefCount)> ActiveWatchers = new();
    private static readonly object WatcherLock = new();

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

        ConnectionSubscriptions.AddOrUpdate(
            Context.ConnectionId,
            _ => [groupName],
            (_, set) =>
            {
                set.Add(groupName);
                return set;
            }
        );

        var lines = rptLogService.ReadFullFile(filePath);
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

        StartOrJoinWatcher(filePath, groupName, serverId, source);
    }

    public async Task UnsubscribeFromLog(string serverId, string source)
    {
        var groupName = $"log:{serverId}:{source}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

        if (ConnectionSubscriptions.TryGetValue(Context.ConnectionId, out var subs))
        {
            subs.Remove(groupName);
        }

        StopOrLeaveWatcher(groupName);
    }

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        if (ConnectionSubscriptions.TryRemove(Context.ConnectionId, out var subs))
        {
            foreach (var groupName in subs)
            {
                StopOrLeaveWatcher(groupName);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    private void StartOrJoinWatcher(string filePath, string groupName, string serverId, string source)
    {
        lock (WatcherLock)
        {
            if (ActiveWatchers.TryGetValue(groupName, out var entry))
            {
                ActiveWatchers[groupName] = (entry.Watcher, entry.RefCount + 1);
                return;
            }

            var watcher = rptLogService.WatchFile(
                filePath,
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
            );

            ActiveWatchers[groupName] = (watcher, 1);
        }
    }

    private static void StopOrLeaveWatcher(string groupName)
    {
        lock (WatcherLock)
        {
            if (!ActiveWatchers.TryGetValue(groupName, out var entry))
            {
                return;
            }

            if (entry.RefCount <= 1)
            {
                entry.Watcher.Dispose();
                ActiveWatchers.TryRemove(groupName, out _);
            }
            else
            {
                ActiveWatchers[groupName] = (entry.Watcher, entry.RefCount - 1);
            }
        }
    }
}
