using System.Collections.Generic;

namespace UKSFWebsite.Api.Models.Mission {
    public class Mission {
        public static int nextId;
        public readonly string descriptionPath;
        public readonly string path;
        public readonly string sqmPath;
        public List<string> descriptionLines;
        public MissionEntity missionEntity;
        public int playerCount;
        public List<string> rawEntities;
        public List<string> sqmLines;

        public Mission(string path) {
            this.path = path;
            descriptionPath = $"{this.path}/description.ext";
            sqmPath = $"{this.path}/mission.sqm";
        }
    }
}
