using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Models.Personnel;

namespace UKSF.Api.Interfaces.Personnel {
    public interface IRolesService : IDataBackedService<IRolesDataService> {
        int Sort(string nameA, string nameB);
        Role GetUnitRoleByOrder(int order);
    }
}
