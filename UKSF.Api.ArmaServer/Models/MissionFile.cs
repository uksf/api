using System.IO;

namespace UKSF.Api.ArmaServer.Models
{
    public class MissionFile
    {
        public string Map;
        public string Name;
        public string Path;

        public MissionFile(FileSystemInfo fileInfo)
        {
            string[] fileNameParts = fileInfo.Name.Split(".");
            Path = fileInfo.Name;
            Name = fileNameParts[0];
            Map = fileNameParts[1];
        }
    }
}
