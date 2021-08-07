using System.Collections.Generic;

namespace UKSF.Api.Personnel.Models
{
    public class RolesDataset
    {
        public IEnumerable<DomainRole> IndividualRoles;
        public IEnumerable<DomainRole> UnitRoles;
    }
}
