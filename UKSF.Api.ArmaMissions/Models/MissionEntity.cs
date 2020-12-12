using System.Collections.Generic;

namespace UKSF.Api.ArmaMissions.Models {
    public class MissionEntity {
        public List<MissionEntityItem> MissionEntityItems = new();
        public int ItemsCount;
    }
}
