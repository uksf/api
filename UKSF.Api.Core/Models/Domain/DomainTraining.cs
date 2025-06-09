namespace UKSF.Api.Core.Models.Domain;

public class DomainTraining : MongoObject
{
    public string Name { get; set; }
    public string ShortName { get; set; }
    public string TeamspeakGroup { get; set; }
}

public class Training
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string ShortName { get; set; }
}
