using System;
using System.Linq;

namespace UKSF.Api.ArmaServer.Npc.Services;

/// Slot numbers are the fact ids. `g1`/`f1` are aliases for `1`.
public static class NpcGuardedFactIds
{
    public static string Normalise(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return string.Empty;

        var s = id.Trim();
        if (s.Length >= 2 && s[0] is 'g' or 'G' or 'f' or 'F' && s[1..].All(char.IsDigit)) return s[1..];

        return s;
    }

    public static bool Same(string a, string b) =>
        string.Equals(Normalise(a), Normalise(b), StringComparison.Ordinal);
}
