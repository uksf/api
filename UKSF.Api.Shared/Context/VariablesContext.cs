﻿using UKSF.Api.Shared.Context.Base;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Extensions;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Shared.Context;

public interface IVariablesContext : IMongoContext<VariableItem>, ICachedMongoContext
{
    Task Update(string key, object value);
}

public class VariablesContext : CachedMongoContext<VariableItem>, IVariablesContext
{
    public VariablesContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) : base(mongoCollectionFactory, eventBus, "variables") { }

    public override VariableItem GetSingle(string key)
    {
        return base.GetSingle(x => x.Key == key.Keyify());
    }

    public async Task Update(string key, object value)
    {
        var variableItem = GetSingle(key);
        if (variableItem == null)
        {
            throw new KeyNotFoundException($"VariableItem with key '{key}' does not exist");
        }

        await base.Update(variableItem.Id, x => x.Item, value);
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

    protected override void SetCache(IEnumerable<VariableItem> newCollection)
    {
        lock (LockObject)
        {
            Cache = newCollection?.OrderBy(x => x.Key).ToList();
        }
    }
}