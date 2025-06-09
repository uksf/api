namespace UKSF.Api.Core.Models.Domain;

public class DomainRank : MongoObject
{
    public string Abbreviation { get; set; }
    public string DiscordRoleId { get; set; }
    public string Name { get; set; }
    public int Order { get; set; }
    public string TeamspeakGroup { get; set; }
}

public class Rank
{
    public string Abbreviation { get; set; }
    public string Id { get; set; }
    public string Name { get; set; }
}
