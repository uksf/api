using UKSF.Api.Core.Context;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;

namespace UKSF.Api.Core.Services;

public interface IVariablesService
{
    VariableItem GetVariable(string key);
    bool GetFeatureState(string featureKey);
}

public class VariablesService(IVariablesContext context) : IVariablesService
{
    public VariableItem GetVariable(string key)
    {
        return context.GetSingle(key);
    }

    public bool GetFeatureState(string featureKey)
    {
        return context.GetSingle($"FEATURE_{featureKey}").AsBoolWithDefault(false);
    }
}
