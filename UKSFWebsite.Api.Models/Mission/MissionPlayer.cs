using UKSFWebsite.Api.Models.Accounts;

namespace UKSFWebsite.Api.Models.Mission {
    public class MissionPlayer {
        public Account account;
        public string name;
        public string objectClass;
        public Rank rank;
        public MissionUnit unit;
    }
}
