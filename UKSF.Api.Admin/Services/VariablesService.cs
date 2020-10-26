using UKSF.Api.Admin.Models;
using UKSF.Api.Admin.Services.Data;
using UKSF.Api.Base.Services.Data;

namespace UKSF.Api.Admin.Services {
    public interface IVariablesService : IDataBackedService<IVariablesDataService> {
        VariableItem GetVariable(string key);
    }

    public class VariablesService : DataBackedService<IVariablesDataService>, IVariablesService {
        public VariablesService(IVariablesDataService data) : base(data) { }

        public VariableItem GetVariable(string key) => Data.GetSingle(key);
    }
}
