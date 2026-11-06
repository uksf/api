namespace UKSF.Api.Integrations.Discord.Services;

public static class DiscordMessageSplitter
{
    public const int MaxLength = 2000;

    public static List<string> Split(string message, int maxLength = MaxLength)
    {
        List<string> chunks = [];
        var current = "";

        foreach (var line in message.Split('\n'))
        {
            var candidate = current.Length == 0 ? line : $"{current}\n{line}";
            if (candidate.Length <= maxLength)
            {
                current = candidate;
                continue;
            }

            if (current.Trim().Length > 0)
            {
                chunks.Add(current);
            }

            current = line;
            while (current.Length > maxLength)
            {
                chunks.Add(current[..maxLength]);
                current = current[maxLength..];
            }
        }

        if (current.Trim().Length > 0)
        {
            chunks.Add(current);
        }

        return chunks;
    }
}
