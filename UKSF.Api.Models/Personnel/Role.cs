namespace UKSF.Api.Models.Personnel {
    public enum RoleType {
        INDIVIDUAL,
        UNIT
    }

    public class Role : MongoObject {
        public string name;
        public int order = 0;
        public RoleType roleType = RoleType.INDIVIDUAL;
    }
}
