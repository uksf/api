using System.Collections.Concurrent;

namespace UKSF.Api.Services;

public interface IAnalyticsRateLimiter
{
    bool IsRateLimited(string visitorId);
}

public class AnalyticsRateLimiter : IAnalyticsRateLimiter
{
    private const int MaxEventsPerMinute = 30;
    private readonly ConcurrentDictionary<string, List<DateTime>> _events = new();

    public bool IsRateLimited(string visitorId)
    {
        var now = DateTime.UtcNow;
        var windowStart = now.AddMinutes(-1);

        var timestamps = _events.GetOrAdd(visitorId, _ => []);
        lock (timestamps)
        {
            timestamps.RemoveAll(t => t < windowStart);
            if (timestamps.Count >= MaxEventsPerMinute)
            {
                return true;
            }

            timestamps.Add(now);
            return false;
        }
    }
}
