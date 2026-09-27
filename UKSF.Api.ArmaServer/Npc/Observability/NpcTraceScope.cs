using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace UKSF.Api.ArmaServer.Npc.Observability;

/// One exact model exchange. Request and Response are the bodies as sent and received.
public sealed record NpcModelCall(string Kind, string Request, string Response, long Ms, string Status);

/// Collects the model calls made while one NPC turn runs. It flows into child tasks, so Jev and
/// the writer running side by side both land in the same turn.
public sealed class NpcTraceScope
{
    private static readonly AsyncLocal<NpcTraceScope> CurrentScope = new();
    private readonly ConcurrentQueue<NpcModelCall> _calls = new();

    public static NpcTraceScope Current => CurrentScope.Value;

    /// The turn being traced, so delivery helpers deep in the broker can report without new parameters.
    public NpcTurnTrace Turn { get; private init; }

    public static IDisposable Begin(NpcTurnTrace turn = null)
    {
        var previous = CurrentScope.Value;
        CurrentScope.Value = new NpcTraceScope { Turn = turn };
        return new Restore(previous);
    }

    public static void Add(NpcModelCall call) => CurrentScope.Value?._calls.Enqueue(call);

    /// Removes and returns the calls of one kind, oldest first.
    public List<NpcModelCall> Take(string kind)
    {
        var all = new List<NpcModelCall>();
        while (_calls.TryDequeue(out var call)) all.Add(call);
        foreach (var other in all.Where(c => c.Kind != kind)) _calls.Enqueue(other);
        return all.Where(c => c.Kind == kind).ToList();
    }

    private sealed class Restore(NpcTraceScope previous) : IDisposable
    {
        public void Dispose() => CurrentScope.Value = previous;
    }
}
