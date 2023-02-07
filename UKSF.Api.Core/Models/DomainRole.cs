namespace UKSF.Api.Core.Models;

public enum RoleType
{
    INDIVIDUAL,
    UNIT
}

public class DomainRole : MongoObject
{
    public string Name { get; set; }
    public int Order { get; set; } = 0;
    public RoleType RoleType { get; set; } = RoleType.INDIVIDUAL;
}

public class Role
{
    public string Id { get; set; }
    public string Name { get; set; }
}
