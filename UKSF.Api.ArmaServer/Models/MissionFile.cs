namespace UKSF.Api.ArmaServer.Models;

public class MissionFile
{
    public MissionFile(FileSystemInfo fileInfo)
    {
        var fileNameParts = fileInfo.Name.Split(".");
        Path = fileInfo.Name;
        Name = fileNameParts[0];
        Map = fileNameParts[1];
    }

    public string Map { get; set; }
    public string Name { get; set; }
    public string Path { get; set; }
}
