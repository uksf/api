using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Core.Models;

public class RolesDataset
{
    public IEnumerable<DomainRole> Roles { get; set; }
}
