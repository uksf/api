using UKSF.Api.Base.Models;

namespace UKSF.Api.Personnel.Models
{
    public class DomainRank : MongoObject
    {
        public string Abbreviation;
        public string DiscordRoleId;
        public string Name;
        public int Order;
        public string TeamspeakGroup;
    }

    public class Rank
    {
        public string Abbreviation;
        public string Id;
        public string Name;
    }
}
