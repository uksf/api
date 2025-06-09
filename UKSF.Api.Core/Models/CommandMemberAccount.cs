using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Core.Models;

public class CommandMemberAccount : MongoObject
{
    public string Firstname { get; set; }
    public string Lastname { get; set; }
    public List<DomainUnit> ParentUnits { get; set; }
    public Qualifications Qualifications { get; set; }
    public List<DomainTraining> Trainings { get; set; }
    public DomainRank Rank { get; set; }
    public DomainRole Role { get; set; }
    public DomainUnit Unit { get; set; }
    public List<DomainUnit> Units { get; set; }
}
