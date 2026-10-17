namespace UKSF.Api.ArmaMissions.Services;

public sealed class SqmClassNode
{
    public string Name { get; init; } = "";
    public int Start { get; init; }
    public int NameStart { get; init; }
    public int NameEnd { get; init; }
    public int BodyClose { get; set; }
    public int End { get; set; }
    public bool HasBody { get; set; }
    public List<SqmClassNode> Classes { get; } = [];
    public List<SqmPropertyNode> Properties { get; } = [];

    public SqmClassNode Class(string name) => Classes.FirstOrDefault(x => x.HasBody && x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public SqmPropertyNode Property(string name) => Properties.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public string Value(string name) => Property(name)?.Text ?? "";

    public string OwnOrAttributeValue(string name)
    {
        var own = Property(name);
        return own is not null ? own.Text : Class("Attributes")?.Value(name) ?? "";
    }

    public IEnumerable<SqmClassNode> Items =>
        Classes.Where(x => x.HasBody && x.Name.StartsWith("Item", StringComparison.OrdinalIgnoreCase) && int.TryParse(x.Name[4..], out _));

    public IEnumerable<SqmClassNode> Descendants => Classes.SelectMany(x => x.Descendants.Prepend(x));
}

public sealed class SqmPropertyNode
{
    public string Name { get; init; } = "";
    public int Start { get; init; }
    public int ValueStart { get; init; }
    public int ValueEnd { get; init; }
    public int End { get; init; }
    public string RawValue { get; init; } = "";

    public string Text => RawValue.Length >= 2 && RawValue[0] == '"' && RawValue[^1] == '"' ? RawValue[1..^1].Replace("\"\"", "\"") : RawValue.Trim();
}

public sealed class SqmSyntaxException(string message) : Exception(message);
