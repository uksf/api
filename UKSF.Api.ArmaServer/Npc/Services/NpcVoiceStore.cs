using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.Npc.Services;

public interface INpcVoiceStore
{
    Task<string> SaveBaseAsync(string voiceId, byte[] wavBytes); // returns relative path
    Task<string> SaveVariantAsync(string voiceId, string mood, byte[] wavBytes);
    Task<string> SaveFillerAsync(string voiceId, string slug, byte[] wavBytes);
    Task<byte[]> ReadAsync(string relativePath);
    bool Exists(string relativePath);
    void Delete(string relativePath);
}

/// Master voice WAVs on disk, one folder per voice:
///   {voiceId}/ref.wav            the uploaded seed
///   {voiceId}/{mood}.wav         generated mood references (neutral.wav, angry.wav, ...)
///   {voiceId}/fillers/{text}.wav filler clips, named by the words said in them
/// A flat dump stops being readable the moment a voice has moods and fillers beside it.
public class NpcVoiceStore(IVariablesService variablesService) : INpcVoiceStore
{
    private static string Sanitise(string part)
    {
        return Regex.Replace(part ?? string.Empty, "[^A-Za-z0-9_-]", "_");
    }

    private string Root()
    {
        var root = variablesService.GetVariable("NPC_VOICE_PATH")?.Item?.ToString();
        if (string.IsNullOrEmpty(root)) throw new InvalidOperationException("NPC_VOICE_PATH not configured");
        return root;
    }

    public static string BasePath(string voiceId) => $"{Sanitise(voiceId)}/ref.wav";
    public static string VariantPath(string voiceId, string mood) => $"{Sanitise(voiceId)}/{Sanitise(mood)}.wav";
    public static string FillerPath(string voiceId, string slug) => $"{Sanitise(voiceId)}/fillers/{slug}.wav";

    private async Task<string> Save(string relativePath, byte[] wavBytes)
    {
        var full = Path.Combine(Root(), relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllBytesAsync(full, wavBytes);
        return relativePath;
    }

    public Task<string> SaveBaseAsync(string voiceId, byte[] wavBytes) => Save(BasePath(voiceId), wavBytes);
    public Task<string> SaveVariantAsync(string voiceId, string mood, byte[] wavBytes) => Save(VariantPath(voiceId, mood), wavBytes);
    public Task<string> SaveFillerAsync(string voiceId, string slug, byte[] wavBytes) => Save(FillerPath(voiceId, slug), wavBytes);

    public async Task<byte[]> ReadAsync(string relativePath)
    {
        var full = Path.Combine(Root(), relativePath);
        if (!File.Exists(full)) return null;
        return await File.ReadAllBytesAsync(full);
    }

    public bool Exists(string relativePath)
    {
        return File.Exists(Path.Combine(Root(), relativePath));
    }

    public void Delete(string relativePath)
    {
        var full = Path.Combine(Root(), relativePath);
        if (File.Exists(full)) File.Delete(full);
    }
}
