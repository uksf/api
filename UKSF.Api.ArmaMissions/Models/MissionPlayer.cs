using UKSF.Api.Personnel.Models;

namespace UKSF.Api.ArmaMissions.Models {
    public class MissionPlayer {
        public Account Account { get; set; }
        public string Name { get; set; }
        public string ObjectClass { get; set; }
        public Rank Rank { get; set; }
        public MissionUnit Unit { get; set; }
    }
}
