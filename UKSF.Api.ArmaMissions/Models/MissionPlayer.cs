using UKSF.Api.Personnel.Models;

namespace UKSF.Api.ArmaMissions.Models
{
    public class MissionPlayer
    {
        public DomainAccount DomainAccount;
        public string Name;
        public string ObjectClass;
        public DomainRank Rank;
        public MissionUnit Unit;
    }
}
