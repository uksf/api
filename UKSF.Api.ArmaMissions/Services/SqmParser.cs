namespace UKSF.Api.ArmaMissions.Services;

public static class SqmParser
{
    private const string Utf8ByteOrderMarkAsLatin1 = "\u00EF\u00BB\u00BF";

    public static SqmClassNode Parse(string text)
    {
        var root = new SqmClassNode { End = text.Length, HasBody = true };
        var index = text.StartsWith(Utf8ByteOrderMarkAsLatin1, StringComparison.Ordinal) ? Utf8ByteOrderMarkAsLatin1.Length : 0;
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
        node.HasBody = true;
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
        var valueEnd = index;
        var depth = 0;
        while (index < text.Length && (depth > 0 || text[index] != ';'))
        {
            if (SkipComment(text, ref index))
            {
                continue;
            }

            switch (text[index])
            {
                case '"':
                    SkipString(text, ref index);
                    valueEnd = index;
                    continue;
                case '{': depth++; break;
                case '}': depth--; break;
            }

            index++;
            if (!char.IsWhiteSpace(text[index - 1]))
            {
                valueEnd = index;
            }
        }

        if (index >= text.Length)
        {
            throw new SqmSyntaxException($"property {name} at offset {start} has no terminating ';'");
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
            else if (text[index] == '#')
            {
                SkipToLineEnd(text, ref index);
            }
            else if (!SkipComment(text, ref index))
            {
                return;
            }
        }
    }

    private static bool SkipComment(string text, ref int index)
    {
        if (text[index] != '/' || index + 1 >= text.Length)
        {
            return false;
        }

        if (text[index + 1] == '/')
        {
            SkipToLineEnd(text, ref index);
            return true;
        }

        if (text[index + 1] == '*')
        {
            var end = text.IndexOf("*/", index + 2, StringComparison.Ordinal);
            index = end < 0 ? text.Length : end + 2;
            return true;
        }

        return false;
    }

    private static void SkipToLineEnd(string text, ref int index)
    {
        while (index < text.Length && text[index] != '\n')
        {
            index++;
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
