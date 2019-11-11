using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Models.Personnel;

namespace UKSFWebsite.Api.Interfaces.Personnel {
    public interface IRolesService {
        IRolesDataService Data();
        int Sort(string nameA, string nameB);
        Role GetUnitRoleByOrder(int order);
    }
}
