using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Core.Context;

public interface IVariablesContext : IMongoContext<DomainVariableItem>
{
    Task Update(string key, object value);
}

public class VariablesContext : MongoContext<DomainVariableItem>, IVariablesContext
{
    public VariablesContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) : base(mongoCollectionFactory, eventBus, "variables") { }

    public async Task Update(string key, object value)
    {
        var variableItem = GetSingle(key);
        if (variableItem == null)
        {
            throw new KeyNotFoundException($"VariableItem with key '{key}' does not exist");
        }

        await base.Update(variableItem.Id, x => x.Item, value);
    }

    public override IEnumerable<DomainVariableItem> Get()
    {
        return base.Get().OrderBy(x => x.Key);
    }

    public override IEnumerable<DomainVariableItem> Get(Func<DomainVariableItem, bool> predicate)
    {
        return base.Get(predicate).OrderBy(x => x.Key);
    }

    public override DomainVariableItem GetSingle(string idOrKey)
    {
        return base.GetSingle(x => x.Id == idOrKey || x.Key == idOrKey.Keyify());
    }

    public override async Task Delete(string key)
    {
        var variableItem = GetSingle(key);
        if (variableItem == null)
        {
            throw new KeyNotFoundException($"VariableItem with key '{key}' does not exist");
        }

        await base.Delete(variableItem);
    }
}
