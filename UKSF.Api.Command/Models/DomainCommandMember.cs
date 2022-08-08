using UKSF.Api.Base.Models;
using UKSF.Api.Personnel.Models;

namespace UKSF.Api.Command.Models;

public class DomainCommandMember : MongoObject
{
    public string Firstname { get; set; }
    public string Lastname { get; set; }
    public List<DomainUnit> ParentUnits { get; set; }
    public Qualifications Qualifications { get; set; }
    public DomainRank Rank { get; set; }
    public DomainRole Role { get; set; }
    public DomainUnit Unit { get; set; }
    public List<DomainUnit> Units { get; set; }
}
