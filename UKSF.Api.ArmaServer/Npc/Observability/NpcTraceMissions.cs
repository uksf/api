using System.Collections.Concurrent;
using System.Reflection;

namespace UKSF.Api.ArmaServer.Npc.Observability;

public interface INpcTraceMissions
{
    /// Remembers mission details; nothing is written until the mission uses an NPC.
    void Started(string session, string mission, string map, string server);

    /// Writes mission.started once, on the first NPC event of the session.
    void EnsureStarted(string session);

    /// Writes mission.ended when the session was traced in this process or has NPC sessions.
    void Ended(string session, bool hadNpcs, object data);
}

public sealed class NpcTraceMissions(INpcTraceRecorder trace) : INpcTraceMissions
{
    private static readonly string ApiVersion = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

    private readonly ConcurrentDictionary<string, object> _pending = new();
    private readonly ConcurrentDictionary<string, byte> _started = new();

    public void Started(string session, string mission, string map, string server)
    {
        if (!string.IsNullOrEmpty(session))
            _pending[session] = new
            {
                mission,
                map,
                server,
                apiVersion = ApiVersion
            };
    }

    public void EnsureStarted(string session)
    {
        if (string.IsNullOrEmpty(session) || !_started.TryAdd(session, 0)) return;
        // After an API restart mid-mission the details are gone; the skill reads that as unknown.
        _pending.TryRemove(session, out var info);
        trace.Record("mission.started", session, info ?? new { apiVersion = ApiVersion });
    }

    public void Ended(string session, bool hadNpcs, object data)
    {
        _pending.TryRemove(session ?? "", out _);
        if (string.IsNullOrEmpty(session) || !(_started.TryRemove(session, out _) || hadNpcs)) return;
        trace.Record("mission.ended", session, data);
    }
}
