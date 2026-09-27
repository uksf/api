using System.Collections.Generic;
using System.Linq;

namespace UKSF.Api.ArmaServer.Npc.Observability;

/// Speech delivery for one turn. Frames and FirstFrameMs are zero for a scripted clip.
public sealed record NpcTtsOutcome(bool Delivered, string Voice, string Text, int Frames, long FirstFrameMs, long TotalMs, string Error)
{
    public static readonly NpcTtsOutcome Skipped = new(false, null, null, 0, 0, 0, "no text");
}

/// The trace of one NPC turn. Decided and Replied write as soon as their stage ends;
/// Finish writes turn.finished once, from the broker's finally block, so every exit is closed.
public sealed class NpcTurnTrace(INpcTraceRecorder trace, string session, string npc, string turn, IReadOnlyList<string> utts)
{
    private bool _finished;

    public string Outcome { get; set; } = "unknown";
    public NpcTtsOutcome Tts { get; set; }
    public string Clip { get; set; }
    public bool? EmoteSent { get; set; }
    public bool? Committed { get; set; }

    public void Decided(string address, bool gaze, object decision = null) =>
        trace.Record(
            "turn.decided",
            session,
            new
            {
                address,
                gaze,
                utts = utts.Count > 0 ? utts : null,
                jev = Calls("jev"),
                decision
            },
            npc,
            turn
        );

    public void Replied(object reply) => trace.Record("turn.replied", session, new { writer = Calls("chat"), reply }, npc, turn);

    public void Finish()
    {
        if (_finished) return;
        _finished = true;
        var tts = Tts is null
            ? null
            : new
            {
                voice = Tts.Voice,
                text = Tts.Text,
                frames = Tts.Frames,
                firstFrameMs = Tts.FirstFrameMs,
                totalMs = Tts.TotalMs,
                delivered = Tts.Delivered,
                error = Tts.Error
            };
        trace.Record(
            "turn.finished",
            session,
            new
            {
                outcome = Outcome,
                tts,
                clip = Clip,
                emoteSent = EmoteSent,
                committed = Committed,
                jev = Calls("jev"),
                writer = Calls("chat")
            },
            npc,
            turn
        );
    }

    private static List<object> Calls(string kind)
    {
        var calls = NpcTraceScope.Current?.Take(kind);
        return calls is { Count: > 0 }
            ? calls.Select(c => (object)new
                       {
                           request = c.Request,
                           response = c.Response,
                           ms = c.Ms,
                           status = c.Status
                       }
                   )
                   .ToList()
            : null;
    }
}
