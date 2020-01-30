using System.Collections.Generic;

namespace UKSF.Api.Models.Mission {
    public class MissionEntity {
        public readonly List<MissionEntityItem> missionEntityItems = new List<MissionEntityItem>();
        public int itemsCount;
    }
}
