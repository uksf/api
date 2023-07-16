using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;

namespace UKSF.Api.Core.Context;

public interface IVariablesContext : IMongoContext<VariableItem>
{
    Task Update(string key, object value);
}

public class VariablesContext : MongoContext<VariableItem>, IVariablesContext
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

    public override IEnumerable<VariableItem> Get()
    {
        return base.Get().OrderBy(x => x.Key);
    }

    public override IEnumerable<VariableItem> Get(Func<VariableItem, bool> predicate)
    {
        return base.Get(predicate).OrderBy(x => x.Key);
    }

    public override VariableItem GetSingle(string idOrKey)
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
