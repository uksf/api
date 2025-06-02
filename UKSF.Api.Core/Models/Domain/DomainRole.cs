namespace UKSF.Api.Core.Models.Domain;

public class DomainRole : MongoObject
{
    public string Name { get; set; }
}

public class Role
{
    public string Id { get; set; }
    public string Name { get; set; }
}
