using System.Text;
using UKSF.Api.ArmaMissions.Exceptions;

namespace UKSF.Api.ArmaMissions.Services;

public static class PboReader
{
    private const uint ProductEntryMethod = 0x56657273;
    private const uint CompressedEntryMethod = 0x43707273;
    private const int MaxLzssExpansion = 9;
    public const long MaxExtractedBytes = 512L * 1024 * 1024;
    public const int MaxEntries = 50_000;
    public const int MaxPathComponents = 200_000;
    private static readonly string[] CanonicalMissionFiles = ["mission.sqm", "description.ext", "cba_settings.sqf"];

    public static void Extract(string pboPath, string folderPath, int maxEntries = MaxEntries, int maxPathComponents = MaxPathComponents)
    {
        byte[] bytes = File.ReadAllBytes(pboPath);
        int position = 0;
        int pathComponents = 0;
        List<Entry> entries = [];
        while (true)
        {
            string name = ReadString(bytes, ref position);
            uint method = ReadUInt(bytes, ref position);
            uint originalSize = ReadUInt(bytes, ref position);
            position += 8;
            uint dataSize = ReadUInt(bytes, ref position);
            if (name.Length == 0 && method == ProductEntryMethod)
            {
                SkipProductProperties(bytes, ref position);
                continue;
            }

            if (name.Length == 0)
            {
                break;
            }

            if (entries.Count == maxEntries)
            {
                throw new PboOperationException($"pbo has more than {maxEntries} files");
            }

            string[] parts = PathParts(name);
            pathComponents += parts.Length;
            if (pathComponents > maxPathComponents)
            {
                throw new PboOperationException($"pbo paths have more than {maxPathComponents} folders and files");
            }

            entries.Add(new Entry(string.Join('\\', parts), parts, method, originalSize, dataSize));
        }

        if (entries.Count == 0)
        {
            throw new PboOperationException($"{Path.GetFileName(pboPath)} contains no files");
        }

        CheckEntries(entries);

        Directory.CreateDirectory(folderPath);
        foreach (Entry entry in entries)
        {
            if (position + entry.DataSize > bytes.Length)
            {
                throw new PboOperationException($"{entry.Name}: data past end of file");
            }

            Span<byte> block = bytes.AsSpan(position, (int)entry.DataSize);
            position += (int)entry.DataSize;
            string outputPath = Path.Combine([folderPath, ..entry.Parts]);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllBytes(outputPath, IsCompressed(entry) ? Decompress(block, entry) : block.ToArray());
        }
    }

    public static byte[] LzssDecode(ReadOnlySpan<byte> input, int outputSize, out int consumed)
    {
        byte[] output = new byte[outputSize];
        int outputPosition = 0;
        int inputPosition = 0;
        while (outputPosition < outputSize)
        {
            byte flags = ReadByte(input, ref inputPosition);
            for (int bit = 0; bit < 8 && outputPosition < outputSize; bit++)
            {
                if ((flags & (1 << bit)) != 0)
                {
                    output[outputPosition++] = ReadByte(input, ref inputPosition);
                    continue;
                }

                byte low = ReadByte(input, ref inputPosition);
                byte high = ReadByte(input, ref inputPosition);
                int distance = low | ((high & 0xF0) << 4);
                int length = (high & 0x0F) + 3;
                for (int i = 0; i < length && outputPosition < outputSize; i++)
                {
                    int source = outputPosition - distance;
                    output[outputPosition++] = source < 0 ? (byte)0x20 : output[source];
                }
            }
        }

        consumed = inputPosition;
        return output;
    }

    private static string[] PathParts(string entryName)
    {
        string[] parts = entryName.Split('\\', '/');
        if (parts.Any(x => x is "" or ".." or "." || x.Contains(':')))
        {
            throw new PboOperationException($"{entryName}: unsafe path in pbo");
        }

        if (parts.Length == 1)
        {
            parts[0] = CanonicalMissionFiles.FirstOrDefault(file => file.Equals(parts[0], StringComparison.OrdinalIgnoreCase)) ?? parts[0];
        }

        return parts;
    }

    private static void CheckEntries(List<Entry> entries)
    {
        IGrouping<string, Entry> duplicate = entries.GroupBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new PboOperationException($"{duplicate.Key}: pbo has more than one entry with this name");
        }

        long total = 0;
        foreach (Entry entry in entries)
        {
            uint size = IsCompressed(entry) ? entry.OriginalSize : entry.DataSize;
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

    private static bool IsCompressed(Entry entry) => entry.Method == CompressedEntryMethod || (entry.OriginalSize != 0 && entry.OriginalSize != entry.DataSize);

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

        uint checksum = BitConverter.ToUInt32(block.Slice(consumed, 4));
        uint unsignedSum = 0;
        uint signedSum = 0;
        foreach (byte value in data)
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

    private static void SkipProductProperties(byte[] bytes, ref int position)
    {
        while (ReadString(bytes, ref position).Length > 0)
        {
            ReadString(bytes, ref position);
        }
    }

    private static string ReadString(byte[] bytes, ref int position)
    {
        int end = Array.IndexOf(bytes, (byte)0, position);
        if (end < 0)
        {
            throw new PboOperationException("pbo header is truncated");
        }

        string value = Encoding.UTF8.GetString(bytes, position, end - position);
        position = end + 1;
        return value;
    }

    private static uint ReadUInt(byte[] bytes, ref int position)
    {
        if (position + 4 > bytes.Length)
        {
            throw new PboOperationException("pbo header is truncated");
        }

        uint value = BitConverter.ToUInt32(bytes, position);
        position += 4;
        return value;
    }

    private static byte ReadByte(ReadOnlySpan<byte> input, ref int position) => input[position++];

    private record Entry(string Name, string[] Parts, uint Method, uint OriginalSize, uint DataSize);
}
