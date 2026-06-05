using System.Text;
using UKSF.Api.ArmaServer.Npc.Models;

namespace UKSF.Api.ArmaServer.Npc.Services;

public static class NpcHistoryFormatter
{
    public static string Format(IEnumerable<NpcHistoryEntry> entries)
    {
        var builder = new StringBuilder();
        foreach (var entry in entries)
        {
            if (builder.Length > 0) builder.Append('\n');
            var prefix = entry.Role == "npc" ? "NPC" : (string.IsNullOrEmpty(entry.Speaker) ? "Player" : entry.Speaker);
            builder.Append(prefix).Append(": ").Append(entry.Text);
        }

        return builder.ToString();
    }
}
