using System.Collections.Concurrent;
using UKSF.Api.Core.Exceptions;

namespace UKSF.Api.Services;

public record PasskeyFlow(string OptionsJson, string AccountId, string Email);

/// <summary>
///     Holds WebAuthn challenges between the options request and the verify request. Each flow can be taken once.
/// </summary>
public class PasskeyFlowStore(TimeProvider timeProvider, int capacity = 10_000)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
    private readonly ConcurrentDictionary<string, (PasskeyFlow Flow, DateTimeOffset Expires)> _flows = new();

    public string Add(PasskeyFlow flow)
    {
        var now = timeProvider.GetUtcNow();
        if (_flows.Count >= capacity)
        {
            foreach (var (flowId, entry) in _flows)
            {
                if (entry.Expires <= now)
                {
                    _flows.TryRemove(flowId, out _);
                }
            }

            if (_flows.Count >= capacity)
            {
                throw new UksfException("Too many passkey requests, please try again shortly", 429);
            }
        }

        var id = Guid.NewGuid().ToString("N");
        _flows[id] = (flow, now + Lifetime);
        return id;
    }

    public PasskeyFlow Take(string flowId)
    {
        if (string.IsNullOrEmpty(flowId) || !_flows.TryRemove(flowId, out var entry) || entry.Expires <= timeProvider.GetUtcNow())
        {
            throw new BadRequestException("Passkey request expired, please try again");
        }

        return entry.Flow;
    }
}
