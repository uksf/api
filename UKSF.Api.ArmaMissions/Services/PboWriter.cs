using System.Security.Cryptography;
using System.Text;
using UKSF.Api.ArmaMissions.Exceptions;

namespace UKSF.Api.ArmaMissions.Services;

public static class PboWriter
{
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

        using var body = new MemoryStream();
        using (var writer = new BinaryWriter(body, Encoding.UTF8, true))
        {
            foreach (var file in files)
            {
                var info = new FileInfo(file.Path);
                WriteAsciiz(writer, file.Name);
                writer.Write(0u);
                writer.Write((uint)info.Length);
                writer.Write((uint)new DateTimeOffset(info.LastWriteTimeUtc).ToUnixTimeSeconds());
                writer.Write(0u);
                writer.Write((uint)info.Length);
            }

            WriteAsciiz(writer, string.Empty);
            writer.Write(new byte[20]);
            foreach (var file in files)
            {
                writer.Write(File.ReadAllBytes(file.Path));
            }
        }

        var bytes = body.ToArray();
        using var output = File.Create(pboPath);
        output.Write(bytes);
        output.WriteByte(0);
        output.Write(SHA1.HashData(bytes));
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
