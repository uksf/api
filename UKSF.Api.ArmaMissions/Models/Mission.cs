using System.Collections.Generic;

namespace UKSF.Api.ArmaMissions.Models {
    public class Mission {
        public static int nextId;
        public readonly string descriptionPath;
        public readonly string path;
        public readonly string sqmPath;
        public List<string> descriptionLines;
        public MissionEntity missionEntity;
        public int playerCount;
        public int maxCurators;
        public List<string> rawEntities;
        public List<string> sqmLines;

        public Mission(string path) {
            this.path = path;
            descriptionPath = $"{this.path}/description.ext";
            sqmPath = $"{this.path}/mission.sqm";
        }
    }
}
