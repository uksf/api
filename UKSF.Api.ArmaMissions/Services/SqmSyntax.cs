namespace UKSF.Api.ArmaMissions.Services;

public sealed class SqmClassNode
{
    public string Name { get; init; } = "";
    public int Start { get; init; }
    public int NameStart { get; init; }
    public int NameEnd { get; init; }
    public int BodyClose { get; set; }
    public int End { get; set; }
    public List<SqmClassNode> Classes { get; } = [];
    public List<SqmPropertyNode> Properties { get; } = [];

    public SqmClassNode Class(string name) => Classes.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public SqmPropertyNode Property(string name) => Properties.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public string Value(string name) => Property(name)?.Text ?? "";

    public string OwnOrAttributeValue(string name)
    {
        var own = Property(name);
        return own is not null ? own.Text : Class("Attributes")?.Value(name) ?? "";
    }

    public IEnumerable<SqmClassNode> Items =>
        Classes.Where(x => x.Name.StartsWith("Item", StringComparison.OrdinalIgnoreCase) && int.TryParse(x.Name[4..], out _));

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

public static class SqmParser
{
    public static SqmClassNode Parse(string text)
    {
        var root = new SqmClassNode { End = text.Length };
        var index = 0;
        ParseBody(text, ref index, root, false);
        root.BodyClose = text.Length;
        return root;
    }

    private static void ParseBody(string text, ref int index, SqmClassNode node, bool braced)
    {
        while (true)
        {
            SkipTrivia(text, ref index);
            if (index >= text.Length)
            {
                if (braced)
                {
                    throw new SqmSyntaxException($"class {node.Name} at offset {node.Start} is never closed");
                }

                return;
            }

            if (text[index] == '}')
            {
                if (!braced)
                {
                    throw new SqmSyntaxException($"unexpected '}}' at offset {index}");
                }

                node.BodyClose = index;
                index++;
                SkipTrivia(text, ref index);
                Expect(text, ref index, ';');
                node.End = index;
                return;
            }

            var start = index;
            var word = ReadIdentifier(text, ref index);
            if (word == "class")
            {
                node.Classes.Add(ParseClass(text, ref index, start));
            }
            else
            {
                node.Properties.Add(ParseProperty(text, ref index, start, word));
            }
        }
    }

    private static SqmClassNode ParseClass(string text, ref int index, int start)
    {
        SkipTrivia(text, ref index);
        var nameStart = index;
        var name = ReadIdentifier(text, ref index);
        var node = new SqmClassNode
        {
            Name = name,
            Start = start,
            NameStart = nameStart,
            NameEnd = index
        };
        SkipTrivia(text, ref index);
        if (index < text.Length && text[index] == ':')
        {
            index++;
            SkipTrivia(text, ref index);
            ReadIdentifier(text, ref index);
            SkipTrivia(text, ref index);
        }

        if (index < text.Length && text[index] == ';')
        {
            index++;
            node.BodyClose = index - 1;
            node.End = index;
            return node;
        }

        Expect(text, ref index, '{');
        ParseBody(text, ref index, node, true);
        return node;
    }

    private static SqmPropertyNode ParseProperty(string text, ref int index, int start, string name)
    {
        SkipTrivia(text, ref index);
        if (index + 1 < text.Length && text[index] == '[' && text[index + 1] == ']')
        {
            index += 2;
            SkipTrivia(text, ref index);
        }

        if (index < text.Length && text[index] == '+')
        {
            index++;
        }

        Expect(text, ref index, '=');
        SkipTrivia(text, ref index);
        var valueStart = index;
        var depth = 0;
        while (index < text.Length && (depth > 0 || text[index] != ';'))
        {
            switch (text[index])
            {
                case '"':
                    SkipString(text, ref index);
                    continue;
                case '{': depth++; break;
                case '}': depth--; break;
            }

            index++;
        }

        if (index >= text.Length)
        {
            throw new SqmSyntaxException($"property {name} at offset {start} has no terminating ';'");
        }

        var valueEnd = index;
        while (valueEnd > valueStart && char.IsWhiteSpace(text[valueEnd - 1]))
        {
            valueEnd--;
        }

        index++;
        return new SqmPropertyNode
        {
            Name = name,
            Start = start,
            ValueStart = valueStart,
            ValueEnd = valueEnd,
            End = index,
            RawValue = text[valueStart..valueEnd]
        };
    }

    private static void SkipString(string text, ref int index)
    {
        index++;
        while (index < text.Length)
        {
            if (text[index] == '"')
            {
                if (index + 1 < text.Length && text[index + 1] == '"')
                {
                    index += 2;
                    continue;
                }

                index++;
                return;
            }

            index++;
        }

        throw new SqmSyntaxException("string is never closed");
    }

    private static void SkipTrivia(string text, ref int index)
    {
        while (index < text.Length)
        {
            if (char.IsWhiteSpace(text[index]))
            {
                index++;
            }
            else if (text[index] == '/' && index + 1 < text.Length && text[index + 1] == '/' || text[index] == '#')
            {
                while (index < text.Length && text[index] != '\n')
                {
                    index++;
                }
            }
            else if (text[index] == '/' && index + 1 < text.Length && text[index + 1] == '*')
            {
                var end = text.IndexOf("*/", index + 2, StringComparison.Ordinal);
                index = end < 0 ? text.Length : end + 2;
            }
            else
            {
                return;
            }
        }
    }

    private static string ReadIdentifier(string text, ref int index)
    {
        var start = index;
        while (index < text.Length && (char.IsLetterOrDigit(text[index]) || text[index] == '_'))
        {
            index++;
        }

        if (index == start)
        {
            throw new SqmSyntaxException($"expected a name at offset {start} but found '{(start < text.Length ? text[start] : ' ')}'");
        }

        return text[start..index];
    }

    private static void Expect(string text, ref int index, char expected)
    {
        if (index >= text.Length || text[index] != expected)
        {
            throw new SqmSyntaxException($"expected '{expected}' at offset {index}");
        }

        index++;
    }
}
