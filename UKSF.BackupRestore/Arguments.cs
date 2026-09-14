namespace UKSF.BackupRestore;

public class Arguments
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    public static Arguments Parse(string[] args)
    {
        var arguments = new Arguments();

        for (var i = 1; i < args.Length - 1; i++)
        {
            if (!args[i].StartsWith("--"))
            {
                continue;
            }

            arguments._values[args[i][2..]] = args[i + 1];
        }

        return arguments;
    }

    public string Optional(string name)
    {
        return _values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
    }

    public string Require(string name)
    {
        if (_values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        throw new ArgumentException($"--{name} is required");
    }
}
