using UKSF.Api.Base.Models;

namespace UKSF.Api.Personnel.Models {
    public enum RoleType {
        INDIVIDUAL,
        UNIT
    }

    public class Role : MongoObject {
        public string Name;
        public int Order = 0;
        public RoleType RoleType = RoleType.INDIVIDUAL;
    }
}
