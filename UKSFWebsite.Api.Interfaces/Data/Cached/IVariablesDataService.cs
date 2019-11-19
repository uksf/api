using System.Threading.Tasks;
using UKSFWebsite.Api.Models.Admin;

namespace UKSFWebsite.Api.Interfaces.Data.Cached {
    public interface IVariablesDataService : IDataService<VariableItem, IVariablesDataService> {
        Task Update(string key, object value);
    }
}
