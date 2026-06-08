using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.Npc.Services;

public interface INpcVoiceStore
{
    Task<string> SaveAsync(string voiceId, byte[] wavBytes); // returns relative path
    Task<byte[]> ReadAsync(string relativePath);
    void Delete(string relativePath);
}

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

    public async Task<string> SaveAsync(string voiceId, byte[] wavBytes)
    {
        var fileName = $"{Sanitise(voiceId)}.wav";
        Directory.CreateDirectory(Root());
        await File.WriteAllBytesAsync(Path.Combine(Root(), fileName), wavBytes);
        return fileName;
    }

    public async Task<byte[]> ReadAsync(string relativePath)
    {
        var full = Path.Combine(Root(), relativePath);
        if (!File.Exists(full)) return null;
        return await File.ReadAllBytesAsync(full);
    }

    public void Delete(string relativePath)
    {
        var full = Path.Combine(Root(), relativePath);
        if (File.Exists(full)) File.Delete(full);
    }
}
