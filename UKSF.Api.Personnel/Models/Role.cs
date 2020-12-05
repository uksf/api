using UKSF.Api.Base.Models;

namespace UKSF.Api.Personnel.Models {
    public enum RoleType {
        INDIVIDUAL,
        UNIT
    }

    public record Role : MongoObject {
        public string Name { get; set; }
        public int Order { get; set; } = 0;
        public RoleType RoleType { get; set; } = RoleType.INDIVIDUAL;
    }
}
