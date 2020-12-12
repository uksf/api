using System.Collections.Generic;

namespace UKSF.Api.ArmaMissions.Models {
    public class Mission {
        public static int NextId;
        public string DescriptionPath;
        public string Path;
        public string SqmPath;
        public List<string> DescriptionLines;
        public int MaxCurators;
        public MissionEntity MissionEntity;
        public int PlayerCount;
        public List<string> RawEntities;
        public List<string> SqmLines;

        public Mission(string path) {
            Path = path;
            DescriptionPath = $"{Path}/description.ext";
            SqmPath = $"{Path}/mission.sqm";
        }
    }
}
