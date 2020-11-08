using UKSF.Api.Personnel.Models;

namespace UKSF.Api.ArmaMissions.Models {
    public class MissionPlayer {
        public Account account;
        public string name;
        public string objectClass;
        public Rank rank;
        public MissionUnit unit;
    }
}
