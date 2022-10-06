namespace UKSF.Api.Shared.Models;

public class RolesDataset
{
    public IEnumerable<DomainRole> IndividualRoles { get; set; }
    public IEnumerable<DomainRole> UnitRoles { get; set; }
}
