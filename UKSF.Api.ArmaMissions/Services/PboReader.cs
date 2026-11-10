using System.Text;
using UKSF.Api.ArmaMissions.Exceptions;

namespace UKSF.Api.ArmaMissions.Services;

public static class PboReader
{
    private const uint ProductEntryMethod = 0x56657273;
    private const uint CompressedEntryMethod = 0x43707273;
    private const int MaxLzssExpansion = 9;
    public const long MaxExtractedBytes = 512L * 1024 * 1024;
    private static readonly string[] CanonicalMissionFiles = ["mission.sqm", "description.ext", "cba_settings.sqf"];

    private record Entry(string Name, uint Method, uint OriginalSize, uint DataSize);

    public static void Extract(string pboPath, string folderPath)
    {
        var bytes = File.ReadAllBytes(pboPath);
        var position = 0;
        List<Entry> entries = [];
        while (true)
        {
            var name = ReadString(bytes, ref position);
            var method = ReadUInt(bytes, ref position);
            var originalSize = ReadUInt(bytes, ref position);
            position += 8;
            var dataSize = ReadUInt(bytes, ref position);
            if (name.Length == 0 && method == ProductEntryMethod)
            {
                SkipProductProperties(bytes, ref position);
                continue;
            }

            if (name.Length == 0)
            {
                break;
            }

            entries.Add(new Entry(CanonicalName(name), method, originalSize, dataSize));
        }

        if (entries.Count == 0)
        {
            throw new PboOperationException($"{Path.GetFileName(pboPath)} contains no files");
        }

        CheckEntries(entries);

        Directory.CreateDirectory(folderPath);
        foreach (var entry in entries)
        {
            if (position + entry.DataSize > bytes.Length)
            {
                throw new PboOperationException($"{entry.Name}: data past end of file");
            }

            var block = bytes.AsSpan(position, (int)entry.DataSize);
            position += (int)entry.DataSize;
            var outputPath = SafeOutputPath(folderPath, entry.Name);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllBytes(outputPath, IsCompressed(entry) ? Decompress(block, entry) : block.ToArray());
        }
    }

    public static byte[] LzssDecode(ReadOnlySpan<byte> input, int outputSize, out int consumed)
    {
        var output = new byte[outputSize];
        var outputPosition = 0;
        var inputPosition = 0;
        while (outputPosition < outputSize)
        {
            var flags = ReadByte(input, ref inputPosition);
            for (var bit = 0; bit < 8 && outputPosition < outputSize; bit++)
            {
                if ((flags & (1 << bit)) != 0)
                {
                    output[outputPosition++] = ReadByte(input, ref inputPosition);
                    continue;
                }

                var low = ReadByte(input, ref inputPosition);
                var high = ReadByte(input, ref inputPosition);
                var distance = low | ((high & 0xF0) << 4);
                var length = (high & 0x0F) + 3;
                for (var i = 0; i < length && outputPosition < outputSize; i++)
                {
                    var source = outputPosition - distance;
                    output[outputPosition++] = source < 0 ? (byte)0x20 : output[source];
                }
            }
        }

        consumed = inputPosition;
        return output;
    }

    private static string CanonicalName(string name)
    {
        return CanonicalMissionFiles.FirstOrDefault(file => file.Equals(name, StringComparison.OrdinalIgnoreCase)) ?? name;
    }

    private static void CheckEntries(List<Entry> entries)
    {
        var duplicate = entries.GroupBy(entry => entry.Name.Replace('/', '\\'), StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new PboOperationException($"{duplicate.Key}: pbo has more than one entry with this name");
        }

        long total = 0;
        foreach (var entry in entries)
        {
            var size = IsCompressed(entry) ? entry.OriginalSize : entry.DataSize;
            if (IsCompressed(entry) && entry.OriginalSize > (long)entry.DataSize * MaxLzssExpansion)
            {
                throw new PboOperationException($"{entry.Name}: declared size {entry.OriginalSize} is too large for {entry.DataSize} compressed bytes");
            }

            total += size;
            if (total > MaxExtractedBytes)
            {
                throw new PboOperationException($"pbo extracts to more than {MaxExtractedBytes} bytes");
            }
        }
    }

    private static bool IsCompressed(Entry entry)
    {
        return entry.Method == CompressedEntryMethod || (entry.OriginalSize != 0 && entry.OriginalSize != entry.DataSize);
    }

    private static byte[] Decompress(ReadOnlySpan<byte> block, Entry entry)
    {
        byte[] data;
        int consumed;
        try
        {
            data = LzssDecode(block, (int)entry.OriginalSize, out consumed);
        }
        catch (IndexOutOfRangeException)
        {
            throw new PboOperationException($"{entry.Name}: compressed data is truncated");
        }

        if (consumed + 4 > block.Length)
        {
            throw new PboOperationException($"{entry.Name}: compressed data has no checksum");
        }

        var checksum = BitConverter.ToUInt32(block.Slice(consumed, 4));
        uint unsignedSum = 0;
        uint signedSum = 0;
        foreach (var value in data)
        {
            unsignedSum += value;
            signedSum += (uint)(sbyte)value;
        }

        if (checksum != unsignedSum && checksum != signedSum)
        {
            throw new PboOperationException($"{entry.Name}: checksum mismatch");
        }

        return data;
    }

    private static string SafeOutputPath(string folderPath, string entryName)
    {
        var parts = entryName.Split('\\', '/');
        if (entryName.StartsWith('\\') || entryName.StartsWith('/') || parts.Any(x => x is ".." or "." || x.Contains(':')))
        {
            throw new PboOperationException($"{entryName}: unsafe path in pbo");
        }

        return Path.Combine([folderPath, ..parts]);
    }

    private static void SkipProductProperties(byte[] bytes, ref int position)
    {
        while (ReadString(bytes, ref position).Length > 0)
        {
            ReadString(bytes, ref position);
        }
    }

    private static string ReadString(byte[] bytes, ref int position)
    {
        var end = Array.IndexOf(bytes, (byte)0, position);
        if (end < 0)
        {
            throw new PboOperationException("pbo header is truncated");
        }

        var value = Encoding.UTF8.GetString(bytes, position, end - position);
        position = end + 1;
        return value;
    }

    private static uint ReadUInt(byte[] bytes, ref int position)
    {
        if (position + 4 > bytes.Length)
        {
            throw new PboOperationException("pbo header is truncated");
        }

        var value = BitConverter.ToUInt32(bytes, position);
        position += 4;
        return value;
    }

    private static byte ReadByte(ReadOnlySpan<byte> input, ref int position)
    {
        return input[position++];
    }
}
