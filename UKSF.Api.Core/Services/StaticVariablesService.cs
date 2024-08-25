using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Core.Services;

public interface IStaticVariablesService : IVariablesService;

public class StaticVariablesService : IStaticVariablesService
{
    private const string UseMemoryDataCacheFeatureKey = "USE_MEMORY_DATA_CACHE";

    public DomainVariableItem GetVariable(string key)
    {
        throw new InvalidOperationException("Static variable service cannot be used for that key");
    }

    public bool GetFeatureState(string featureKey)
    {
        if (featureKey == UseMemoryDataCacheFeatureKey)
        {
            return true;
        }

        throw new InvalidOperationException("Static variable service cannot be used for that feature key");
    }
}
