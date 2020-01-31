namespace UKSF.Api.Models.Personnel {
    public class Rank : MongoObject {
        public string abbreviation;
        public string discordRoleId;
        public string name;
        public int order = 0;
        public string teamspeakGroup;
    }
}
