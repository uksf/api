using System.Collections.Generic;
using UKSF.Api.Models.Personnel;

namespace UKSF.Api.Interfaces.Data.Cached {
    public interface IRolesDataService : IDataService<Role, IRolesDataService> {
        new List<Role> Get();
        new Role GetSingle(string name);
    }
}