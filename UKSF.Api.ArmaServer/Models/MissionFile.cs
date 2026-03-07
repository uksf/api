namespace UKSF.Api.ArmaServer.Models;

public class MissionFile
{
    public MissionFile(FileSystemInfo fileInfo)
    {
        Path = fileInfo.Name;
        Size = fileInfo is FileInfo fi ? fi.Length : 0;
        LastModified = fileInfo.LastWriteTimeUtc;

        // Arma mission files follow the convention: name.map.pbo
        var nameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(fileInfo.Name);
        var lastDot = nameWithoutExtension.LastIndexOf('.');
        if (lastDot > 0)
        {
            Name = nameWithoutExtension[..lastDot];
            Map = nameWithoutExtension[(lastDot + 1)..];
        }
        else
        {
            Name = nameWithoutExtension;
            Map = string.Empty;
        }
    }

    public string Map { get; set; }
    public string Name { get; set; }
    public string Path { get; set; }
    public long Size { get; set; }
    public DateTime LastModified { get; set; }
}
