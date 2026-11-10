using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UKSF.Api.ArmaMissions.Exceptions;

namespace UKSF.Api.ArmaMissions.Services;

public sealed partial class RapDecoder
{
    public const int MaxDepth = 64;
    public const int MaxNodes = 2_000_000;
    public const int MaxOutputChars = 64 * 1024 * 1024;

    private static readonly byte[] Signature = [0, (byte)'r', (byte)'a', (byte)'P'];
    private readonly byte[] _bytes;
    private readonly StringBuilder _text = new();
    private int _nodes;

    private RapDecoder(byte[] bytes)
    {
        _bytes = bytes;
    }

    public static bool IsRapified(ReadOnlySpan<byte> bytes)
    {
        return bytes.StartsWith(Signature);
    }

    public static string ToText(byte[] bytes)
    {
        if (!IsRapified(bytes))
        {
            throw new PboOperationException("file is not rapified");
        }

        var decoder = new RapDecoder(bytes);
        try
        {
            var position = 16;
            decoder.ReadAsciiz(ref position);
            decoder.WriteBody(position, 0);
        }
        catch (IndexOutOfRangeException)
        {
            throw new PboOperationException("rapified file is truncated");
        }
        catch (ArgumentException)
        {
            throw new PboOperationException("rapified file is truncated");
        }

        return decoder._text.ToString();
    }

    private void WriteBody(int position, int depth)
    {
        EnterLevel(depth);
        var indent = new string('\t', depth);
        var count = ReadCount(ref position);
        for (var i = 0; i < count; i++)
        {
            CountNode();
            var type = _bytes[position++];
            switch (type)
            {
                case 0:
                {
                    var name = ReadAsciiz(ref position);
                    var bodyPosition = ReadOffset(ref position);
                    var parent = ReadAsciiz(ref bodyPosition);
                    Append(indent, "class ", name);
                    if (parent.Length > 0)
                    {
                        Append(": ", parent);
                    }

                    Append("\n", indent, "{\n");
                    WriteBody(bodyPosition, depth + 1);
                    Append(indent, "};\n");
                    break;
                }
                case 1:
                {
                    var subType = _bytes[position++];
                    var name = ReadAsciiz(ref position);
                    Append(indent, name, " = ", ReadScalar(ref position, subType), ";\n");
                    break;
                }
                case 2:
                case 5:
                {
                    var append = type == 5;
                    if (append)
                    {
                        position += 4;
                    }

                    var name = ReadAsciiz(ref position);
                    Append(indent, name, append ? "[] += " : "[] = ", ReadArray(ref position, depth + 1), ";\n");
                    break;
                }
                case 3:  Append(indent, "class ", ReadAsciiz(ref position), ";\n"); break;
                case 4:  Append(indent, "delete ", ReadAsciiz(ref position), ";\n"); break;
                default: throw new PboOperationException($"unknown rapified entry type {type} at offset {position - 1}");
            }
        }
    }

    private string ReadArray(ref int position, int depth)
    {
        EnterLevel(depth);
        var count = ReadCount(ref position);
        var items = new List<string>();
        for (var i = 0; i < count; i++)
        {
            CountNode();
            var type = _bytes[position++];
            items.Add(type == 3 ? ReadArray(ref position, depth + 1) : ReadScalar(ref position, type));
        }

        return $"{{{string.Join(",", items)}}}";
    }

    private string ReadScalar(ref int position, byte type)
    {
        switch (type)
        {
            case 0:
            case 4: return QuoteConfigString(ReadAsciiz(ref position));
            case 1:
            {
                var value = BitConverter.ToSingle(_bytes, position);
                position += 4;
                return value.ToString("R", CultureInfo.InvariantCulture).Replace('E', 'e');
            }
            case 2:
            {
                var value = BitConverter.ToInt32(_bytes, position);
                position += 4;
                return value.ToString(CultureInfo.InvariantCulture);
            }
            case 6:
            {
                var value = BitConverter.ToInt64(_bytes, position);
                position += 8;
                return value.ToString(CultureInfo.InvariantCulture);
            }
            default: throw new PboOperationException($"unknown rapified value type {type} at offset {position - 1}");
        }
    }

    public static string QuoteConfigString(string value)
    {
        var lines = LineBreak().Split(value).Select(line => line.Replace("\"", "\"\""));
        return $"\"{string.Join("\" \\n \"", lines)}\"";
    }

    private int ReadCount(ref int position)
    {
        var value = ReadCompressedInt(ref position);
        if (value > _bytes.Length - position)
        {
            throw new PboOperationException($"rapified count {value} at offset {position} exceeds the file");
        }

        return (int)value;
    }

    private uint ReadCompressedInt(ref int position)
    {
        uint value = 0;
        for (var shift = 0; shift < 35; shift += 7)
        {
            uint part = _bytes[position++];
            if (shift == 28 && part > 0x0F)
            {
                throw new PboOperationException($"rapified integer at offset {position - 1} overflows");
            }

            value |= (part & 0x7F) << shift;
            if ((part & 0x80) == 0)
            {
                return value;
            }
        }

        throw new PboOperationException($"rapified integer at offset {position - 1} overflows");
    }

    private int ReadOffset(ref int position)
    {
        var offset = BitConverter.ToUInt32(_bytes, position);
        position += 4;
        if (offset < 16 || offset >= _bytes.Length)
        {
            throw new PboOperationException($"rapified class offset {offset} is outside the file");
        }

        return (int)offset;
    }

    private static void EnterLevel(int depth)
    {
        if (depth > MaxDepth)
        {
            throw new PboOperationException($"rapified file nests deeper than {MaxDepth}");
        }
    }

    private void CountNode()
    {
        if (++_nodes > MaxNodes)
        {
            throw new PboOperationException($"rapified file has more than {MaxNodes} entries");
        }
    }

    private void Append(params string[] parts)
    {
        foreach (var part in parts)
        {
            _text.Append(part);
        }

        if (_text.Length > MaxOutputChars)
        {
            throw new PboOperationException($"rapified file decodes to more than {MaxOutputChars} characters");
        }
    }

    private string ReadAsciiz(ref int position)
    {
        var end = Array.IndexOf(_bytes, (byte)0, position);
        if (end < 0)
        {
            throw new PboOperationException("rapified string is not terminated");
        }

        var value = Encoding.UTF8.GetString(_bytes, position, end - position);
        position = end + 1;
        return value;
    }

    [GeneratedRegex("\r\n|\r|\n")]
    private static partial Regex LineBreak();
}
