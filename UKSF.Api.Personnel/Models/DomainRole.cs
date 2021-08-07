using UKSF.Api.Base.Models;

namespace UKSF.Api.Personnel.Models
{
    public enum RoleType
    {
        INDIVIDUAL,
        UNIT
    }

    public class DomainRole : MongoObject
    {
        public string Name;
        public int Order = 0;
        public RoleType RoleType = RoleType.INDIVIDUAL;
    }

    public class Role
    {
        public string Id;
        public string Name;
    }
}
