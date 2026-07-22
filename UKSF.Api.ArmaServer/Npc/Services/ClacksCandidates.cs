namespace UKSF.Api.ArmaServer.Npc.Services;

// clacks now serves MODELS, not roles: the caller owns the model + fallback list. The npc usages
// keep their intent here — chat prefers the local 9B (ultron, then iultron), offloads to iultron's
// npc-profile 9B, then cloud haiku; voice prefers the always-on pockettts on the dedi itself.
public static class ClacksCandidates
{
    public const string NpcChatModel = "qwen3.5-9b";
    public static readonly string[] NpcChatFallbacks = ["qwen3.5-9b-npc", "haiku"];

    public const string VoiceModel = "pockettts";
    public static readonly string[] VoiceNodes = ["server", "ultron", "iultron"];

    public const string EmoteModel = "indextts2";

    // Warm the chat primary + the dedi's voice engine while a session is live.
    public static readonly string[] WarmModels = [NpcChatModel, VoiceModel];
}
