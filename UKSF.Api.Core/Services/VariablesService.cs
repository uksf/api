using UKSF.Api.Core.Context;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Core.Services;

public interface IVariablesService
{
    DomainVariableItem GetVariable(string key);
    bool GetFeatureState(string featureKey);
}

public class VariablesService(IVariablesContext context) : IVariablesService
{
    public DomainVariableItem GetVariable(string key)
    {
        return context.GetSingle(key);
    }

    public bool GetFeatureState(string featureKey)
    {
        return context.GetSingle($"FEATURE_{featureKey}").AsBoolWithDefault(false);
    }
}
