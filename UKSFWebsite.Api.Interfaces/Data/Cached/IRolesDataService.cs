using System.Collections.Generic;
using UKSFWebsite.Api.Models.Personnel;

namespace UKSFWebsite.Api.Interfaces.Data.Cached {
    public interface IRolesDataService : IDataService<Role, IRolesDataService> {
        new List<Role> Get();
        new Role GetSingle(string name);
    }
}
