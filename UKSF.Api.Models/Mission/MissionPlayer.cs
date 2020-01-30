using UKSF.Api.Models.Personnel;

namespace UKSF.Api.Models.Mission {
    public class MissionPlayer {
        public Account account;
        public string name;
        public string objectClass;
        public Rank rank;
        public MissionUnit unit;
    }
}
