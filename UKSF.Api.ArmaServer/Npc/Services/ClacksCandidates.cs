namespace UKSF.Api.ArmaServer.Npc.Services;

// clacks now serves MODELS, not roles: the caller owns the model + fallback list. The npc usages
// keep their intent here — chat runs on luna, which needs no desktop up, and falls back to the
// local 9B; voice prefers the always-on pockettts on the dedi itself.
public static class ClacksCandidates
{
    public const string NpcChatModel = "luna";

    // Reasoning effort is pinned low. Left unset the gpt-5.x models spend heavily on hidden
    // reasoning, which buys nothing on a two-sentence NPC line.
    public const string NpcChatEffort = "low";

    public static readonly string[] NpcChatFallbacks = ["qwen3.5-9b", "qwen3.5-9b-npc", "haiku"];

    public const string VoiceModel = "pockettts";
    public static readonly string[] VoiceNodes = ["server", "ultron", "iultron"];

    public const string EmoteModel = "indextts2";

    // Warm the voice engine while a session is live. The chat primary is a cloud model with no
    // load to pay for, and warming the local fallback would hold a GPU for a model that may never
    // be reached.
    public static readonly string[] WarmModels = [VoiceModel];
}
