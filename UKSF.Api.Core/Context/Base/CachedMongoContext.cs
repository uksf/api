using System.Linq.Expressions;
using MongoDB.Driver;
using MoreLinq;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Core.Context.Base;

public interface ICachedMongoContext
{
    void Refresh();
}

public class CachedMongoContext<T> : MongoContextBase<T>, IMongoContext<T>, ICachedMongoContext where T : MongoObject
{
    private const string UseMemoryDataCacheFeatureKey = "USE_MEMORY_DATA_CACHE";
    private readonly IEventBus _eventBus;
    private readonly object _lockObject = new();
    private readonly IVariablesService _variablesService;

    protected CachedMongoContext(
        IMongoCollectionFactory mongoCollectionFactory,
        IEventBus eventBus,
        IVariablesService variablesService,
        string collectionName
    ) : base(mongoCollectionFactory, collectionName)
    {
        _eventBus = eventBus;
        _variablesService = variablesService;
    }

    public List<T> Cache { get; private set; }

    public void Refresh()
    {
        SetCache(base.Get());
    }

    public override IEnumerable<T> Get()
    {
        if (!UseCache())
        {
            return base.Get();
        }

        if (Cache != null)
        {
            return Cache;
        }

        SetCache(base.Get());
        return Cache;
    }

    public override IEnumerable<T> Get(Func<T, bool> predicate)
    {
        if (!UseCache())
        {
            return base.Get(predicate);
        }

        if (Cache == null)
        {
            Get();
        }

        return Cache!.Where(predicate);
    }

    public override T GetSingle(string id)
    {
        if (!UseCache())
        {
            return base.GetSingle(id);
        }

        if (Cache == null)
        {
            Get();
        }

        return Cache!.FirstOrDefault(x => x.Id == id);
    }

    public override T GetSingle(Func<T, bool> predicate)
    {
        if (!UseCache())
        {
            return base.GetSingle(predicate);
        }

        if (Cache == null)
        {
            Get();
        }

        return Cache!.FirstOrDefault(predicate);
    }

    public override async Task Add(T item)
    {
        await base.Add(item);
        SetCache(base.Get());
        DataAddEvent(item);
    }

    public override async Task Update<TField>(string id, Expression<Func<T, TField>> fieldSelector, TField value)
    {
        await base.Update(id, fieldSelector, value);
        Refresh(); // TODO: intelligent refresh
        DataUpdateEvent(id);
    }

    // TODO: Should this return the updated object? Probably
    public override async Task Update(string id, UpdateDefinition<T> update)
    {
        await base.Update(id, update);
        Refresh(); // TODO: intelligent refresh
        DataUpdateEvent(id);
    }

    public override async Task Update(Expression<Func<T, bool>> filterExpression, UpdateDefinition<T> update)
    {
        await base.Update(filterExpression, update);
        Refresh(); // TODO: intelligent refresh
        DataUpdateEvent(GetSingle(filterExpression.Compile()).Id);
    }

    public override async Task UpdateMany(Expression<Func<T, bool>> filterExpression, UpdateDefinition<T> update)
    {
        await base.UpdateMany(filterExpression, update);
        Refresh(); // TODO: intelligent refresh
        Get(filterExpression.Compile()).ForEach(x => DataUpdateEvent(x.Id));
    }

    public override async Task FindAndUpdate(Expression<Func<T, bool>> filterExpression, UpdateDefinition<T> update)
    {
        await base.FindAndUpdate(filterExpression, update);
        Refresh(); // TODO: intelligent refresh
        DataUpdateEvent(GetSingle(filterExpression.Compile()).Id);
    }

    public override async Task Replace(T item)
    {
        await base.Replace(item);
        SetCache(base.Get());
        DataUpdateEvent(item.Id);
    }

    public override async Task Delete(string id)
    {
        await base.Delete(id);
        SetCache(base.Get());
        DataDeleteEvent(id);
    }

    public override async Task Delete(T item)
    {
        await base.Delete(item);
        SetCache(base.Get());
        DataDeleteEvent(item.Id);
    }

    public override async Task DeleteMany(Expression<Func<T, bool>> filterExpression)
    {
        var ids = Get(filterExpression.Compile()).ToList();
        await base.DeleteMany(filterExpression);
        SetCache(base.Get());
        ids.ForEach(x => DataDeleteEvent(x.Id));
    }

    private void SetCache(IEnumerable<T> newCollection)
    {
        lock (_lockObject)
        {
            Cache = newCollection?.ToList();
        }
    }

    private bool UseCache()
    {
        return _variablesService.GetFeatureState(UseMemoryDataCacheFeatureKey);
    }

    private void DataAddEvent(T item)
    {
        DataEvent(new(EventType.ADD, new ContextEventData<T>(string.Empty, item)));
    }

    private void DataUpdateEvent(string id)
    {
        DataEvent(new(EventType.UPDATE, new ContextEventData<T>(id, null)));
    }

    private void DataDeleteEvent(string id)
    {
        DataEvent(new(EventType.DELETE, new ContextEventData<T>(id, null)));
    }

    protected virtual void DataEvent(EventModel eventModel)
    {
        _eventBus.Send(eventModel);
    }
}
