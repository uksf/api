using System.Text;

namespace UKSF.Api.ArmaServer.Npc.Services;

public static class NpcTextSanitiser
{
    private const int MaxLength = 500;

    public static string Sanitise(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var builder = new StringBuilder(input.Length);
        var lastWasSpace = false;
        foreach (var c in input)
        {
            // Control chars (incl. newline/tab) -> single space; collapse runs.
            var isSpace = char.IsControl(c) || char.IsWhiteSpace(c);
            if (isSpace)
            {
                if (!lastWasSpace) builder.Append(' ');
                lastWasSpace = true;
            }
            else
            {
                builder.Append(c);
                lastWasSpace = false;
            }
        }

        var result = builder.ToString().Trim();
        return result.Length > MaxLength ? result[..MaxLength] : result;
    }
}
