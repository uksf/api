using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace UKSF.Api.ArmaMissions.Tests.Services;

public static class PboFixture
{
    public static string Write(string pboPath, params (string Name, uint Method, byte[] Data, uint OriginalSize)[] entries)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.ASCII, true))
        {
            writer.Write(new byte[1]);
            writer.Write(0x56657273u);
            writer.Write(new byte[16]);
            writer.Write(Encoding.ASCII.GetBytes("prefix\0op\0\0"));
            foreach (var entry in entries)
            {
                writer.Write(Encoding.ASCII.GetBytes(entry.Name + "\0"));
                writer.Write(entry.Method);
                writer.Write(entry.OriginalSize);
                writer.Write(0L);
                writer.Write((uint)entry.Data.Length);
            }

            writer.Write(new byte[21]);
            foreach (var entry in entries)
            {
                writer.Write(entry.Data);
            }
        }

        File.WriteAllBytes(pboPath, stream.ToArray());
        return pboPath;
    }

    public static Dictionary<string, string> ReadTree(string folder)
    {
        return Directory.GetFiles(folder, "*", SearchOption.AllDirectories).ToDictionary(x => Path.GetRelativePath(folder, x), File.ReadAllText);
    }
}
