using System.Globalization;
using System.Text;
using UKSF.Api.ArmaMissions.Exceptions;

namespace UKSF.Api.ArmaMissions.Services;

public static class RapDecoder
{
    private static readonly byte[] Signature = [0, (byte)'r', (byte)'a', (byte)'P'];

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

        var text = new StringBuilder();
        try
        {
            var position = 16;
            ReadAsciiz(bytes, ref position);
            WriteBody(bytes, position, text, 0);
        }
        catch (IndexOutOfRangeException)
        {
            throw new PboOperationException("rapified file is truncated");
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new PboOperationException("rapified file is truncated");
        }

        return text.ToString();
    }

    private static void WriteBody(byte[] bytes, int position, StringBuilder text, int depth)
    {
        var indent = new string('\t', depth);
        var count = ReadCompressedInt(bytes, ref position);
        for (var i = 0; i < count; i++)
        {
            var type = bytes[position++];
            switch (type)
            {
                case 0:
                {
                    var name = ReadAsciiz(bytes, ref position);
                    var offset = (int)ReadUInt(bytes, ref position);
                    var bodyPosition = offset;
                    var parent = ReadAsciiz(bytes, ref bodyPosition);
                    text.Append(indent).Append("class ").Append(name);
                    if (parent.Length > 0)
                    {
                        text.Append(": ").Append(parent);
                    }

                    text.Append('\n').Append(indent).Append("{\n");
                    WriteBody(bytes, bodyPosition, text, depth + 1);
                    text.Append(indent).Append("};\n");
                    break;
                }
                case 1:
                {
                    var subType = bytes[position++];
                    var name = ReadAsciiz(bytes, ref position);
                    text.Append(indent).Append(name).Append(" = ").Append(ReadScalar(bytes, ref position, subType)).Append(";\n");
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

                    var name = ReadAsciiz(bytes, ref position);
                    text.Append(indent).Append(name).Append(append ? "[] += " : "[] = ").Append(ReadArray(bytes, ref position)).Append(";\n");
                    break;
                }
                case 3:  text.Append(indent).Append("class ").Append(ReadAsciiz(bytes, ref position)).Append(";\n"); break;
                case 4:  text.Append(indent).Append("delete ").Append(ReadAsciiz(bytes, ref position)).Append(";\n"); break;
                default: throw new PboOperationException($"unknown rapified entry type {type} at offset {position - 1}");
            }
        }
    }

    private static string ReadArray(byte[] bytes, ref int position)
    {
        var count = ReadCompressedInt(bytes, ref position);
        var items = new string[count];
        for (var i = 0; i < count; i++)
        {
            var type = bytes[position++];
            items[i] = type == 3 ? ReadArray(bytes, ref position) : ReadScalar(bytes, ref position, type);
        }

        return $"{{{string.Join(",", items)}}}";
    }

    private static string ReadScalar(byte[] bytes, ref int position, byte type)
    {
        switch (type)
        {
            case 0:
            case 4: return $"\"{ReadAsciiz(bytes, ref position).Replace("\"", "\"\"")}\"";
            case 1:
            {
                var value = BitConverter.ToSingle(bytes, position);
                position += 4;
                return value.ToString("R", CultureInfo.InvariantCulture).Replace('E', 'e');
            }
            case 2:
            {
                var value = BitConverter.ToInt32(bytes, position);
                position += 4;
                return value.ToString(CultureInfo.InvariantCulture);
            }
            case 6:
            {
                var value = BitConverter.ToInt64(bytes, position);
                position += 8;
                return value.ToString(CultureInfo.InvariantCulture);
            }
            default: throw new PboOperationException($"unknown rapified value type {type} at offset {position - 1}");
        }
    }

    private static int ReadCompressedInt(byte[] bytes, ref int position)
    {
        var value = 0;
        var shift = 0;
        while (true)
        {
            var part = bytes[position++];
            value |= (part & 0x7F) << shift;
            if ((part & 0x80) == 0)
            {
                return value;
            }

            shift += 7;
        }
    }

    private static uint ReadUInt(byte[] bytes, ref int position)
    {
        var value = BitConverter.ToUInt32(bytes, position);
        position += 4;
        return value;
    }

    private static string ReadAsciiz(byte[] bytes, ref int position)
    {
        var end = Array.IndexOf(bytes, (byte)0, position);
        if (end < 0)
        {
            throw new PboOperationException("rapified string is not terminated");
        }

        var value = Encoding.UTF8.GetString(bytes, position, end - position);
        position = end + 1;
        return value;
    }
}
