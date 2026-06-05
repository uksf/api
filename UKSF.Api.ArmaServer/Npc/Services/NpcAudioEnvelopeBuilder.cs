using System.Collections.Generic;
using System.Text;

namespace UKSF.Api.ArmaServer.Npc.Services;

public static class NpcAudioEnvelopeBuilder
{
    // Whole command must stay < 64 KB (listener.rs rejects > 65536). 48 KB of base64
    // leaves generous headroom for the SQF wrapper, ids, and any quote-doubling.
    private const int DefaultChunkSize = 49152;

    public static List<string> BuildAudio(string npcId, string turnId, string audioBase64, long durationMs, int chunkSize = DefaultChunkSize) =>
        BuildChunked("npc_audio", [Quote(npcId), Quote(turnId)], audioBase64, durationMs, chunkSize);

    public static List<string>
        BuildFiller(string npcId, string voiceId, string fillerId, string audioBase64, long durationMs, int chunkSize = DefaultChunkSize) =>
        BuildChunked("npc_filler", [Quote(npcId), Quote(voiceId), Quote(fillerId)], audioBase64, durationMs, chunkSize);

    private static List<string> BuildChunked(string type, string[] leadingFields, string audioBase64, long durationMs, int chunkSize)
    {
        var chunks = Chunk(audioBase64, chunkSize);
        var total = chunks.Count;
        var commands = new List<string>(total);
        for (var index = 0; index < total; index++)
        {
            var lead = string.Join(",", leadingFields);
            commands.Add($"[\"{type}\",{lead},{index},{total},\"{Escape(chunks[index])}\",{durationMs}]");
        }

        return commands;
    }

    private static List<string> Chunk(string data, int chunkSize)
    {
        if (data.Length == 0) return [""];
        var chunks = new List<string>((data.Length / chunkSize) + 1);
        for (var start = 0; start < data.Length; start += chunkSize)
        {
            var length = Math.Min(chunkSize, data.Length - start);
            chunks.Add(data.Substring(start, length));
        }

        return chunks;
    }

    private static string Quote(string value) => $"\"{Escape(value)}\"";

    private static string Escape(string value)
    {
        if (!value.Contains('"')) return value;
        var builder = new StringBuilder(value.Length + 8);
        foreach (var c in value) builder.Append(c == '"' ? "\"\"" : c.ToString());
        return builder.ToString();
    }
}
