namespace UKSF.Api.ArmaServer.Services;

public interface ILogSubscriptionService : IDisposable
{
    bool AddSubscription(string connectionId, string groupName);
    void RemoveSubscription(string connectionId, string groupName);
    List<string> RemoveAllSubscriptions(string connectionId);
    void StartOrJoinWatcher(string groupName, Func<IDisposable> watcherFactory);
    void StopOrLeaveWatcher(string groupName);
}

public class LogSubscriptionService : ILogSubscriptionService
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, HashSet<string>> _connectionSubscriptions = new();
    private readonly Dictionary<string, IDisposable> _watchers = new();
    private readonly Dictionary<string, int> _watcherRefCounts = new();
    private bool _disposed;

    public bool AddSubscription(string connectionId, string groupName)
    {
        lock (_lock)
        {
            if (!_connectionSubscriptions.TryGetValue(connectionId, out var subs))
            {
                subs = [];
                _connectionSubscriptions[connectionId] = subs;
            }

            return subs.Add(groupName);
        }
    }

    public void RemoveSubscription(string connectionId, string groupName)
    {
        lock (_lock)
        {
            if (_connectionSubscriptions.TryGetValue(connectionId, out var subs))
            {
                subs.Remove(groupName);
            }
        }
    }

    public List<string> RemoveAllSubscriptions(string connectionId)
    {
        lock (_lock)
        {
            if (_connectionSubscriptions.Remove(connectionId, out var subs))
            {
                return subs.ToList();
            }

            return [];
        }
    }

    public void StartOrJoinWatcher(string groupName, Func<IDisposable> watcherFactory)
    {
        lock (_lock)
        {
            if (_watcherRefCounts.TryGetValue(groupName, out var refCount))
            {
                _watcherRefCounts[groupName] = refCount + 1;
                return;
            }

            var watcher = watcherFactory();
            _watchers[groupName] = watcher;
            _watcherRefCounts[groupName] = 1;
        }
    }

    public void StopOrLeaveWatcher(string groupName)
    {
        lock (_lock)
        {
            if (!_watcherRefCounts.TryGetValue(groupName, out var refCount))
            {
                return;
            }

            refCount--;
            if (refCount <= 0)
            {
                _watchers[groupName].Dispose();
                _watchers.Remove(groupName);
                _watcherRefCounts.Remove(groupName);
            }
            else
            {
                _watcherRefCounts[groupName] = refCount;
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            foreach (var watcher in _watchers.Values)
            {
                watcher.Dispose();
            }

            _watchers.Clear();
            _watcherRefCounts.Clear();
            _connectionSubscriptions.Clear();
        }
    }
}
