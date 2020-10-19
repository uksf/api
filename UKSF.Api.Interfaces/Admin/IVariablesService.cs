using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Models.Admin;

namespace UKSF.Api.Interfaces.Admin {
    public interface IVariablesService : IDataBackedService<IVariablesDataService> {
        VariableItem GetVariable(string key);
    }
}
