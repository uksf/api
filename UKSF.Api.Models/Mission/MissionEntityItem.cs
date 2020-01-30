using System.Collections.Generic;

namespace UKSF.Api.Models.Mission {
    public class MissionEntityItem {
        public static double position = 10;
        public bool isPlayable;
        public string itemType;
        public MissionEntity missionEntity;
        public List<string> rawMissionEntities = new List<string>();
        public List<string> rawMissionEntityItem = new List<string>();
    }
}
