using System.Collections.Generic;

namespace UKSFWebsite.Api.Models.Mission {
    public class MissionEntity {
        public readonly List<MissionEntityItem> missionEntityItems = new List<MissionEntityItem>();
        public int itemsCount;
    }
}
