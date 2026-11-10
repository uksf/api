using System.Text;

namespace UKSF.Api.Integrations.Teamspeak.Query;

public class TeamspeakQueryException(int id, string message) : Exception($"TeamSpeak query error {id}: {message}")
{
    public int Id { get; } = id;
}

public static class TeamspeakQueryProtocol
{
    public const int EmptyResultErrorId = 1281;

    private static readonly (char Raw, string Escaped)[] Escapes =
    [
        ('\\', "\\\\"),
        ('/', "\\/"),
        (' ', "\\s"),
        ('|', "\\p"),
        ('\a', "\\a"),
        ('\b', "\\b"),
        ('\f', "\\f"),
        ('\n', "\\n"),
        ('\r', "\\r"),
        ('\t', "\\t"),
        ('\v', "\\v")
    ];

    public static string Escape(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            var escape = Array.Find(Escapes, x => x.Raw == character);
            if (escape.Escaped is null)
            {
                builder.Append(character);
            }
            else
            {
                builder.Append(escape.Escaped);
            }
        }

        return builder.ToString();
    }

    public static string Unescape(string value)
    {
        var builder = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != '\\' || i + 1 == value.Length)
            {
                builder.Append(value[i]);
                continue;
            }

            var escaped = value.Substring(i, 2);
            var escape = Array.Find(Escapes, x => x.Escaped == escaped);
            builder.Append(escape.Escaped is null ? value[i + 1] : escape.Raw);
            i++;
        }

        return builder.ToString();
    }

    public static IReadOnlyList<IReadOnlyDictionary<string, string>> ParseRecords(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return [];
        }

        return line.Split('|').Select(ParseProperties).ToList();
    }

    public static void ThrowIfError(string errorLine)
    {
        var error = ParseProperties(errorLine["error ".Length..]);
        var id = int.Parse(error["id"]);
        if (id != 0)
        {
            throw new TeamspeakQueryException(id, error.GetValueOrDefault("msg", string.Empty));
        }
    }

    private static IReadOnlyDictionary<string, string> ParseProperties(string record)
    {
        Dictionary<string, string> properties = [];
        foreach (var property in record.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = property.IndexOf('=');
            if (separator < 0)
            {
                properties[property] = string.Empty;
            }
            else
            {
                properties[property[..separator]] = Unescape(property[(separator + 1)..]);
            }
        }

        return properties;
    }
}
