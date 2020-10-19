using UKSF.Api.Interfaces.Admin;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Models.Admin;

namespace UKSF.Api.Services.Admin {
    public class VariablesService : DataBackedService<IVariablesDataService>, IVariablesService {
        public VariablesService(IVariablesDataService data) : base(data) { }

        public VariableItem GetVariable(string key) => Data.GetSingle(key);
    }
}
