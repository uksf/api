using System.Collections.Generic;

namespace UKSF.Api.Models.Mission {
    public class MissionEntityItem {
        public static double position = 10;
        public static double curatorPosition = 0.5;
        public bool isPlayable;
        public string dataType;
        public string type;
        public MissionEntity missionEntity;
        public List<string> rawMissionEntities = new List<string>();
        public List<string> rawMissionEntityItem = new List<string>();
    }
}
