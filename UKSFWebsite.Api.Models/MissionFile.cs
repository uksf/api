using System.IO;

namespace UKSFWebsite.Api.Models {
    public class MissionFile {
        public string map;
        public string name;
        public string path;

        public MissionFile(FileSystemInfo fileInfo) {
            string[] fileNameParts = fileInfo.Name.Split(".");
            path = fileInfo.Name;
            name = fileNameParts[0];
            map = fileNameParts[1];
        }
    }
}
