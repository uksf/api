using UKSF.Api.Core.Context;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;

namespace UKSF.Api.Core.Services;

public interface IVariablesService
{
    VariableItem GetVariable(string key);
    bool GetFeatureState(string featureKey);
}

public class VariablesService : IVariablesService
{
    private readonly IVariablesContext _context;

    public VariablesService(IVariablesContext context)
    {
        _context = context;
    }

    public VariableItem GetVariable(string key)
    {
        return _context.GetSingle(key);
    }

    public bool GetFeatureState(string featureKey)
    {
        return _context.GetSingle($"FEATURE_{featureKey}").AsBoolWithDefault(false);
    }
}
