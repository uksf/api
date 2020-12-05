using UKSF.Api.Base.Models;

namespace UKSF.Api.Personnel.Models {
    public record Rank : MongoObject {
        public string Abbreviation { get; set; }
        public string DiscordRoleId { get; set; }
        public string Name { get; set; }
        public int Order { get; set; }
        public string TeamspeakGroup { get; set; }
    }
}
