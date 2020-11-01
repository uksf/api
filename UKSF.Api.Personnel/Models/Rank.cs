using UKSF.Api.Base.Models;

namespace UKSF.Api.Personnel.Models {
    public class Rank : DatabaseObject {
        public string abbreviation;
        public string discordRoleId;
        public string name;
        public int order;
        public string teamspeakGroup;
    }
}
