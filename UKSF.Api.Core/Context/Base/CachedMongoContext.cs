using System.Linq.Expressions;
using MongoDB.Driver;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Core.Context.Base;

public class ContextCache<T> where T : MongoObject
{
    private List<T> _data = [];
    private volatile bool _dataInitialized;
    public bool DataInitialized => _dataInitialized;
    public List<T> Data => _data;

    public void SetData(IEnumerable<T> newCollection)
    {
        var newCache = newCollection.ToList();
        Interlocked.Exchange(ref _data, newCache);

        _dataInitialized = true;
    }

    public void UpdateItem(T item)
    {
        var newList = new List<T>(_data);
        var index = newList.FindIndex(x => x.Id == item.Id);
        if (index >= 0)
        {
            newList[index] = item;
        }
        else
        {
            newList.Add(item);
        }

        Interlocked.Exchange(ref _data, newList);
    }

    public void RemoveItem(string id)
    {
        var newList = new List<T>(_data);
        newList.RemoveAll(x => x.Id == id);
        Interlocked.Exchange(ref _data, newList);
    }
}

public interface ICachedMongoContext
{
    void Refresh();
}

public class CachedMongoContext<T> : MongoContextBase<T>, IMongoContext<T>, ICachedMongoContext where T : MongoObject
{
    private const string UseMemoryDataCacheFeatureKey = "USE_MEMORY_DATA_CACHE";
    private readonly ContextCache<T> _cache = new();
    private readonly IEventBus _eventBus;
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

    public void Refresh()
    {
        _cache.SetData(OrderCollection(base.Get()));
    }

    public override IEnumerable<T> Get()
    {
        if (!_cache.DataInitialized)
        {
            Refresh();
        }

        return UseCache() ? _cache.Data : OrderCollection(base.Get());
    }

    public override IEnumerable<T> Get(Func<T, bool> predicate)
    {
        return UseCache() ? Get().Where(predicate) : OrderCollection(base.Get(predicate));
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
        Refresh();
        DataAddEvent(item);
    }

    public override async Task Update<TField>(string id, Expression<Func<T, TField>> fieldSelector, TField value)
    {
        await base.Update(id, fieldSelector, value);
        RefreshSingleItem(id);
        DataUpdateEvent(id);
    }

    public override async Task Update(string id, UpdateDefinition<T> update)
    {
        await base.Update(id, update);
        RefreshSingleItem(id);
        DataUpdateEvent(id);
    }

    public override async Task Update(Expression<Func<T, bool>> filterExpression, UpdateDefinition<T> update)
    {
        await base.Update(filterExpression, update);
        Refresh();
        var match = GetSingle(filterExpression.Compile());
        if (match is not null)
        {
            DataUpdateEvent(match.Id);
        }
    }

    public override async Task UpdateMany(Expression<Func<T, bool>> filterExpression, UpdateDefinition<T> update)
    {
        var ids = Get(filterExpression.Compile()).ToList();
        await base.UpdateMany(filterExpression, update);
        Refresh();
        ids.ForEach(x => DataUpdateEvent(x.Id));
    }

    public override async Task FindAndUpdate(Expression<Func<T, bool>> filterExpression, UpdateDefinition<T> update)
    {
        await base.FindAndUpdate(filterExpression, update);
        Refresh();
        var match = GetSingle(filterExpression.Compile());
        if (match is not null)
        {
            DataUpdateEvent(match.Id);
        }
    }

    public override async Task Replace(T item)
    {
        await base.Replace(item);
        UpdateCacheItem(item);
        DataUpdateEvent(item.Id);
    }

    public override async Task Delete(string id)
    {
        await base.Delete(id);
        _cache.RemoveItem(id);
        DataDeleteEvent(id);
    }

    public override async Task Delete(T item)
    {
        await base.Delete(item);
        _cache.RemoveItem(item.Id);
        DataDeleteEvent(item.Id);
    }

    public override async Task DeleteMany(Expression<Func<T, bool>> filterExpression)
    {
        var ids = Get(filterExpression.Compile()).ToList();
        await base.DeleteMany(filterExpression);
        Refresh();
        ids.ForEach(x => DataDeleteEvent(x.Id));
    }

    protected virtual IEnumerable<T> OrderCollection(IEnumerable<T> collection)
    {
        return collection;
    }

    private void RefreshSingleItem(string id)
    {
        var updated = base.GetSingle(id);
        if (updated is not null)
        {
            UpdateCacheItem(updated);
        }
        else
        {
            _cache.RemoveItem(id);
        }
    }

    private void UpdateCacheItem(T item)
    {
        _cache.UpdateItem(item);
        ReorderCache();
    }

    private void ReorderCache()
    {
        _cache.SetData(OrderCollection(_cache.Data));
    }

    private bool UseCache()
    {
        return _variablesService.GetFeatureState(UseMemoryDataCacheFeatureKey);
    }

    private void DataAddEvent(T item)
    {
        DataEvent(EventType.Add, new ContextEventData<T>(string.Empty, item));
    }

    private void DataUpdateEvent(string id)
    {
        DataEvent(EventType.Update, new ContextEventData<T>(id, null));
    }

    private void DataDeleteEvent(string id)
    {
        DataEvent(EventType.Delete, new ContextEventData<T>(id, null));
    }

    protected virtual void DataEvent(EventType eventType, EventData eventData)
    {
        _eventBus.Send(new EventModel(eventType, eventData, $"{CollectionName}.{eventType}"));
    }
}
