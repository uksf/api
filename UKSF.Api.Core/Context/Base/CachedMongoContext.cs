using System.Collections.Concurrent;
using System.Linq.Expressions;
using MongoDB.Driver;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Core.Context.Base;

public class ContextCache<T> where T : MongoObject
{
    private ConcurrentBag<T> _data = new();
    public bool DataInitialized { get; private set; }
    public ConcurrentBag<T> Data => _data;

    public void SetData(IEnumerable<T> newCollection)
    {
        var newCache = new ConcurrentBag<T>(newCollection);
        Interlocked.Exchange(ref _data, newCache);

        if (!DataInitialized)
        {
            DataInitialized = true;
        }
    }
}

public interface ICachedMongoContext
{
    void Refresh(string source);
}

public class CachedMongoContext<T> : MongoContextBase<T>, IMongoContext<T>, ICachedMongoContext where T : MongoObject
{
    private const string UseMemoryDataCacheFeatureKey = "USE_MEMORY_DATA_CACHE";
    private readonly ContextCache<T> _cache = new();
    private readonly IEventBus _eventBus;
    private readonly IVariablesService _variablesService;
    private readonly string _collectionName;
    private readonly string _id = Guid.NewGuid().ToString();

    protected CachedMongoContext(
        IMongoCollectionFactory mongoCollectionFactory,
        IEventBus eventBus,
        IVariablesService variablesService,
        string collectionName
    ) : base(mongoCollectionFactory, collectionName)
    {
        _collectionName = collectionName;
        _eventBus = eventBus;
        _variablesService = variablesService;
    }

    public void Refresh(string source)
    {
        var logger = StaticServiceProvider.ServiceProvider?.GetService<IUksfLogger>();
        if (logger != null)
        {
            logger.LogDebug($"Refresh cached data for Collection: {_collectionName} | ID: {_id} | Source: {source}");
            if (_collectionName == "accounts")
            {
                var cached = _cache.Data.FirstOrDefault(x => x.Id == "59e38f10594c603b78aa9dbd");
                var database = base.Get().FirstOrDefault(x => x.Id == "59e38f10594c603b78aa9dbd");
                logger.LogDebug($"Cached {cached} - Database {database}");
            }
        }

        _cache.SetData(base.Get());
    }

    public override IEnumerable<T> Get()
    {
        if (!_cache.DataInitialized)
        {
            Refresh("get");
        }

        return UseCache() ? _cache.Data : base.Get();
    }

    public override IEnumerable<T> Get(Func<T, bool> predicate)
    {
        return UseCache() ? Get().Where(predicate) : base.Get(predicate);
    }

    public override T GetSingle(string id)
    {
        return UseCache() ? Get().FirstOrDefault(x => x.Id == id) : base.GetSingle(id);
    }

    public override T GetSingle(Func<T, bool> predicate)
    {
        return UseCache() ? Get().FirstOrDefault(predicate) : base.GetSingle(predicate);
    }

    public override async Task Add(T item)
    {
        await base.Add(item);
        Refresh("add");
        DataAddEvent(item);
    }

    public override async Task Update<TField>(string id, Expression<Func<T, TField>> fieldSelector, TField value)
    {
        await base.Update(id, fieldSelector, value);
        Refresh("update 1"); // TODO: intelligent refresh
        DataUpdateEvent(id);
    }

    // TODO: Should this return the updated object? Probably
    public override async Task Update(string id, UpdateDefinition<T> update)
    {
        await base.Update(id, update);
        Refresh("update 2"); // TODO: intelligent refresh
        DataUpdateEvent(id);
    }

    public override async Task Update(Expression<Func<T, bool>> filterExpression, UpdateDefinition<T> update)
    {
        await base.Update(filterExpression, update);
        Refresh("update 3"); // TODO: intelligent refresh
        DataUpdateEvent(GetSingle(filterExpression.Compile()).Id);
    }

    public override async Task UpdateMany(Expression<Func<T, bool>> filterExpression, UpdateDefinition<T> update)
    {
        var ids = Get(filterExpression.Compile()).ToList();
        await base.UpdateMany(filterExpression, update);
        Refresh("update many"); // TODO: intelligent refresh
        ids.ForEach(x => DataUpdateEvent(x.Id));
    }

    public override async Task FindAndUpdate(Expression<Func<T, bool>> filterExpression, UpdateDefinition<T> update)
    {
        await base.FindAndUpdate(filterExpression, update);
        Refresh("find and update"); // TODO: intelligent refresh
        DataUpdateEvent(GetSingle(filterExpression.Compile()).Id);
    }

    public override async Task Replace(T item)
    {
        await base.Replace(item);
        Refresh("replace");
        DataUpdateEvent(item.Id);
    }

    public override async Task Delete(string id)
    {
        await base.Delete(id);
        Refresh("delete");
        DataDeleteEvent(id);
    }

    public override async Task Delete(T item)
    {
        await base.Delete(item);
        Refresh("delete");
        DataDeleteEvent(item.Id);
    }

    public override async Task DeleteMany(Expression<Func<T, bool>> filterExpression)
    {
        var ids = Get(filterExpression.Compile()).ToList();
        await base.DeleteMany(filterExpression);
        Refresh("delete many");
        ids.ForEach(x => DataDeleteEvent(x.Id));
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
