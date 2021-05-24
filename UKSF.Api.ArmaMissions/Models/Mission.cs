using System.Collections.Generic;

namespace UKSF.Api.ArmaMissions.Models
{
    public class Mission
    {
        public static int NextId;
        public List<string> DescriptionLines;
        public string DescriptionPath;
        public int MaxCurators;
        public MissionEntity MissionEntity;
        public string Path;
        public int PlayerCount;
        public List<string> RawEntities;
        public List<string> SqmLines;
        public string SqmPath;

        public Mission(string path)
        {
            Path = path;
            DescriptionPath = $"{Path}/description.ext";
            SqmPath = $"{Path}/mission.sqm";
        }
    }
}
