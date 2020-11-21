using UKSF.Api.Admin.Context;
using UKSF.Api.Admin.Models;

namespace UKSF.Api.Admin.Services {
    public interface IVariablesService {
        VariableItem GetVariable(string key);
    }

    public class VariablesService : IVariablesService {
        private readonly IVariablesContext _context;

        public VariablesService(IVariablesContext context) => _context = context;

        public VariableItem GetVariable(string key) => _context.GetSingle(key);
    }
}
