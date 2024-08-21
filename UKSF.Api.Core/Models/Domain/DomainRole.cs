namespace UKSF.Api.Core.Models.Domain;

public enum RoleType
{
    Individual,
    Unit
}

public class DomainRole : MongoObject
{
    public string Name { get; set; }
    public int Order { get; set; } = 0;
    public RoleType RoleType { get; set; } = RoleType.Individual;
}

public class Role
{
    public string Id { get; set; }
    public string Name { get; set; }
}
