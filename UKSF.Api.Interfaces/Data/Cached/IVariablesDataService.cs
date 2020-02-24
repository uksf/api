using System.Threading.Tasks;
using UKSF.Api.Models.Admin;

namespace UKSF.Api.Interfaces.Data.Cached {
    public interface IVariablesDataService : IDataService<VariableItem, IVariablesDataService>, ICachedDataService {
        Task Update(string key, object value);
    }
}
