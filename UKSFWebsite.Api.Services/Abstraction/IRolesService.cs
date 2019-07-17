using System.Collections.Generic;
using UKSFWebsite.Api.Models;

namespace UKSFWebsite.Api.Services.Abstraction {
    public interface IRolesService : IDataService<Role> {
        new List<Role> Get();
        new Role GetSingle(string name);
        int Sort(string nameA, string nameB);
        Role GetUnitRoleByOrder(int order);
    }
}
