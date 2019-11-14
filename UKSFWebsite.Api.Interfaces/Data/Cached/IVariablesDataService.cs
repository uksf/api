using System.Threading.Tasks;
using UKSFWebsite.Api.Models.Admin;
using UKSFWebsite.Api.Models.Utility;

namespace UKSFWebsite.Api.Interfaces.Data.Cached {
    public interface IVariablesDataService : IDataService<VariableItem> {
        Task Update(string key, object value);
    }
}
