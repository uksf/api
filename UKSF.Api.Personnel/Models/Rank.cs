using UKSF.Api.Base.Models;

namespace UKSF.Api.Personnel.Models
{
    public class Rank : MongoObject
    {
        public string Abbreviation;
        public string DiscordRoleId;
        public string Name;
        public int Order;
        public string TeamspeakGroup;
    }
}
