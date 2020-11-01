using UKSF.Api.Base.Models;

namespace UKSF.Api.Personnel.Models {
    public enum RoleType {
        INDIVIDUAL,
        UNIT
    }

    public class Role : DatabaseObject {
        public string name;
        public int order = 0;
        public RoleType roleType = RoleType.INDIVIDUAL;
    }
}
