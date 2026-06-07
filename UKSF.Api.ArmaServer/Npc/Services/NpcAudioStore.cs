using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.Npc.Services;

public interface INpcAudioStore
{
    Task<string> SaveAsync(string sessionId, string npcId, string clipId, byte[] wavBytes);
    Task<byte[]> ReadAsync(string relativePath);
}

/// <summary>
/// WAV clips live on disk under NPC_AUDIO_PATH, foldered by UTC date; mongo stores the relative
/// path. Files deliberately survive mission end — they are the browsable session voice-line archive.
/// Filename components come from game events, so they are sanitised before touching the filesystem.
/// </summary>
public class NpcAudioStore(IVariablesService variablesService) : INpcAudioStore
{
    private static string Sanitise(string part)
    {
        return Regex.Replace(part ?? string.Empty, "[^A-Za-z0-9_-]", "_");
    }

    private string Root()
    {
        var root = variablesService.GetVariable("NPC_AUDIO_PATH")?.Item?.ToString();
        if (string.IsNullOrEmpty(root)) throw new InvalidOperationException("NPC_AUDIO_PATH not configured");
        return root;
    }

    public async Task<string> SaveAsync(string sessionId, string npcId, string clipId, byte[] wavBytes)
    {
        var folder = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var fileName = $"{Sanitise(sessionId)}_{Sanitise(npcId)}_{Sanitise(clipId)}.wav";
        var directory = Path.Combine(Root(), folder);
        Directory.CreateDirectory(directory);
        await File.WriteAllBytesAsync(Path.Combine(directory, fileName), wavBytes);
        return $"{folder}/{fileName}";
    }

    public async Task<byte[]> ReadAsync(string relativePath)
    {
        var full = Path.Combine(Root(), relativePath);
        if (!File.Exists(full)) return null;
        return await File.ReadAllBytesAsync(full);
    }
}
