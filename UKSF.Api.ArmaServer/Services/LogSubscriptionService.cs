using System.Collections.Concurrent;

namespace UKSF.Api.ArmaServer.Services;

public interface ILogSubscriptionService : IDisposable
{
    void AddSubscription(string connectionId, string groupName);
    void RemoveSubscription(string connectionId, string groupName);
    List<string> RemoveAllSubscriptions(string connectionId);
    void StartOrJoinWatcher(string groupName, Func<IDisposable> watcherFactory);
    void StopOrLeaveWatcher(string groupName);
}

public class LogSubscriptionService : ILogSubscriptionService
{
    private readonly ConcurrentDictionary<string, ConcurrentHashSet> _connectionSubscriptions = new();
    private readonly Dictionary<string, WatcherEntry> _activeWatchers = new();
    private readonly object _watcherLock = new();
    private bool _disposed;

    public void AddSubscription(string connectionId, string groupName)
    {
        var subs = _connectionSubscriptions.GetOrAdd(connectionId, _ => new ConcurrentHashSet());
        subs.Add(groupName);
    }

    public void RemoveSubscription(string connectionId, string groupName)
    {
        if (_connectionSubscriptions.TryGetValue(connectionId, out var subs))
        {
            subs.Remove(groupName);
        }
    }

    public List<string> RemoveAllSubscriptions(string connectionId)
    {
        if (_connectionSubscriptions.TryRemove(connectionId, out var subs))
        {
            return subs.ToList();
        }

        return [];
    }

    public void StartOrJoinWatcher(string groupName, Func<IDisposable> watcherFactory)
    {
        lock (_watcherLock)
        {
            if (_activeWatchers.TryGetValue(groupName, out var entry))
            {
                entry.RefCount++;
                return;
            }

            var watcher = watcherFactory();
            _activeWatchers[groupName] = new WatcherEntry(watcher);
        }
    }

    public void StopOrLeaveWatcher(string groupName)
    {
        lock (_watcherLock)
        {
            if (!_activeWatchers.TryGetValue(groupName, out var entry))
            {
                return;
            }

            entry.RefCount--;
            if (entry.RefCount <= 0)
            {
                entry.Dispose();
                _activeWatchers.Remove(groupName);
            }
        }
    }

    public void Dispose()
    {
        lock (_watcherLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            foreach (var entry in _activeWatchers.Values)
            {
                entry.Dispose();
            }

            _activeWatchers.Clear();
        }

        _connectionSubscriptions.Clear();
    }

    private sealed class WatcherEntry(IDisposable watcher) : IDisposable
    {
        public int RefCount { get; set; } = 1;

        public void Dispose()
        {
            watcher.Dispose();
        }
    }

    private sealed class ConcurrentHashSet
    {
        private readonly ConcurrentDictionary<string, byte> _dictionary = new();

        public void Add(string item) => _dictionary.TryAdd(item, 0);
        public void Remove(string item) => _dictionary.TryRemove(item, out _);
        public int Count => _dictionary.Count;
        public List<string> ToList() => _dictionary.Keys.ToList();
    }
}
