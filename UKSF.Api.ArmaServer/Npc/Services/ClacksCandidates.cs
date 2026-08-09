namespace UKSF.Api.ArmaServer.Npc.Services;

// clacks now serves MODELS, not roles: the caller owns the model + fallback list. The npc usages
// keep their intent here — chat runs on luna, which needs no desktop up, and falls back to the
// local 9B; voice runs the same clone engine on every node, so a mission maker hears in testing
// what players hear live.
public static class ClacksCandidates
{
    public const string NpcChatModel = "luna";

    // Reasoning effort is pinned low. Left unset the gpt-5.x models spend heavily on hidden
    // reasoning, which buys nothing on a two-sentence NPC line.
    public const string NpcChatEffort = "low";

    public static readonly string[] NpcChatFallbacks = ["qwen3.5-9b", "haiku"];

    public const string VoiceModel = "pockettts";

    // Desktop, then laptop, then dedi. The engine is CPU-bound and the dedi's pinned BelowNormal
    // cores run it about 2.5x slower than the desktop (measured 1.54x realtime against 3.9x), so
    // the dedi is the backup for when no personal machine is up.
    public static readonly string[] VoiceNodes = ["ultron", "iultron", "server"];

    public const string EmoteModel = "indextts2";

    // Only the GPU boxes run indextts2. Unpinned, a local refusal (governor slot)
    // walks the whole peer list and can land on a node that cannot serve it.
    public static readonly string[] EmoteNodes = ["ultron", "iultron"];

    // Warm the voice engine while a session is live. The chat primary is a cloud model with no
    // load to pay for, and warming the local fallback would hold a GPU for a model that may never
    // be reached.
    public static readonly string[] WarmModels = [VoiceModel];
}
