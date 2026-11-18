using System.Security.Cryptography;
using System.Text;
using UKSF.Api.ArmaMissions.Exceptions;

namespace UKSF.Api.ArmaMissions.Services;

public static class PboWriter
{
    private const uint CompressedEntryMethod = 0x43707273;

    private static readonly HashSet<string> CompressibleExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".sqf",
        ".sqs",
        ".ext",
        ".hpp",
        ".h",
        ".cpp",
        ".inc",
        ".fsm",
        ".bikb",
        ".xml",
        ".csv",
        ".json",
        ".html",
        ".cfg"
    };

    public static void Pack(string folderPath, string pboPath, IReadOnlyCollection<string> excludePatterns)
    {
        var files = Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories)
                             .Select(path => (Path: path, Name: Path.GetRelativePath(folderPath, path).Replace('/', '\\')))
                             .Where(file => !excludePatterns.Any(pattern => Matches(Path.GetFileName(file.Path), pattern)))
                             .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
                             .ToList();
        if (files.Count == 0)
        {
            throw new PboOperationException($"{folderPath} has no files to pack");
        }

        var entries = files.Select(file => (file.Name, Info: new FileInfo(file.Path), Stored: Stored(file.Path))).ToList();
        using var body = new MemoryStream();
        using (var writer = new BinaryWriter(body, Encoding.UTF8, true))
        {
            foreach (var entry in entries)
            {
                var compressed = entry.Stored.Length != entry.Info.Length;
                WriteAsciiz(writer, entry.Name);
                writer.Write(compressed ? CompressedEntryMethod : 0u);
                writer.Write((uint)entry.Info.Length);
                writer.Write(0u);
                writer.Write((uint)new DateTimeOffset(entry.Info.LastWriteTimeUtc).ToUnixTimeSeconds());
                writer.Write((uint)entry.Stored.Length);
            }

            WriteAsciiz(writer, string.Empty);
            writer.Write(new byte[20]);
            foreach (var entry in entries)
            {
                writer.Write(entry.Stored);
            }
        }

        var bytes = body.ToArray();
        using var output = File.Create(pboPath);
        output.Write(bytes);
        output.WriteByte(0);
        output.Write(SHA1.HashData(bytes));
    }

    private static byte[] Stored(string path)
    {
        var raw = File.ReadAllBytes(path);
        if (raw.Length == 0 || !CompressibleExtensions.Contains(Path.GetExtension(path)))
        {
            return raw;
        }

        var compressed = PboLzss.Encode(raw);
        return compressed.Length < raw.Length ? compressed : raw;
    }

    private static bool Matches(string fileName, string pattern)
    {
        return pattern.StartsWith("*.", StringComparison.Ordinal)
            ? fileName.EndsWith(pattern[1..], StringComparison.OrdinalIgnoreCase)
            : fileName.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteAsciiz(BinaryWriter writer, string value)
    {
        writer.Write(Encoding.UTF8.GetBytes(value));
        writer.Write((byte)0);
    }
}
