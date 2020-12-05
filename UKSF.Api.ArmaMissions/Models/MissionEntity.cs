using System.Collections.Generic;

namespace UKSF.Api.ArmaMissions.Models {
    public class MissionEntity {
        public List<MissionEntityItem> MissionEntityItems { get; set; } = new();
        public int ItemsCount { get; set; }
    }
}
