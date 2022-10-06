using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Extensions;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Shared.Services;

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
