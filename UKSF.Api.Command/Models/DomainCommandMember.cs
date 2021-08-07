using System.Collections.Generic;
using UKSF.Api.Base.Models;
using UKSF.Api.Personnel.Models;

namespace UKSF.Api.Command.Models
{
    public class DomainCommandMember : MongoObject
    {
        public string Firstname;
        public string Lastname;
        public List<DomainUnit> ParentUnits;
        public DomainRank Rank;
        public DomainRole Role;
        public DomainUnit Unit;
        public List<DomainUnit> Units;
    }
}
