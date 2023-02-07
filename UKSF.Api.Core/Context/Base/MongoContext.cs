using System.Linq.Expressions;
using MongoDB.Driver;
using MoreLinq;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;
using SortDirection = UKSF.Api.Core.Models.SortDirection;

namespace UKSF.Api.Core.Context.Base;

public interface IMongoContext<T> where T : MongoObject
{
    IEnumerable<T> Get();
    IEnumerable<T> Get(Func<T, bool> predicate);

    PagedResult<T> GetPaged(
        int page,
        int pageSize,
        SortDirection sortDirection,
        string sortField,
        IEnumerable<Expression<Func<T, object>>> filterPropertSelectors,
        string filter
    );

    PagedResult<TOut> GetPaged<TOut>(
        int page,
        int pageSize,
        Func<MongoDB.Driver.IMongoCollection<T>, IAggregateFluent<TOut>> aggregator,
        SortDefinition<TOut> sortDefinition,
        FilterDefinition<TOut> filterDefinition
    );

    T GetSingle(string id);
    T GetSingle(Func<T, bool> predicate);
    Task Add(T item);
    Task Update(string id, Expression<Func<T, object>> fieldSelector, object value);
    Task Update(string id, UpdateDefinition<T> update);
    Task Update(Expression<Func<T, bool>> filterExpression, UpdateDefinition<T> update);
    Task UpdateMany(Expression<Func<T, bool>> filterExpression, UpdateDefinition<T> update);
    Task FindAndUpdate(Expression<Func<T, bool>> filterExpression, UpdateDefinition<T> update);
    Task Replace(T item);
    Task Delete(string id);
    Task Delete(T item);
    Task DeleteMany(Expression<Func<T, bool>> filterExpression);
    FilterDefinition<TFilter> BuildPagedComplexQuery<TFilter>(string query, Func<string, FilterDefinition<TFilter>> filter);
}

public class MongoContext<T> : MongoContextBase<T>, IMongoContext<T> where T : MongoObject
{
    private readonly IEventBus _eventBus;

    protected MongoContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, string collectionName) : base(
        mongoCollectionFactory,
        collectionName
    )
    {
        _eventBus = eventBus;
    }

    public override async Task Add(T item)
    {
        await base.Add(item);
        DataAddEvent(item);
    }

    public override async Task Update(string id, Expression<Func<T, object>> fieldSelector, object value)
    {
        await base.Update(id, fieldSelector, value);
        DataUpdateEvent(id);
    }

    public override async Task Update(string id, UpdateDefinition<T> update)
    {
        await base.Update(id, update);
        DataUpdateEvent(id);
    }

    public override async Task Update(Expression<Func<T, bool>> filterExpression, UpdateDefinition<T> update)
    {
        await base.Update(filterExpression, update);
        DataUpdateEvent(GetSingle(filterExpression.Compile()).Id);
    }

    public override async Task UpdateMany(Expression<Func<T, bool>> filterExpression, UpdateDefinition<T> update)
    {
        await base.UpdateMany(filterExpression, update);
        Get(filterExpression.Compile()).ForEach(x => DataUpdateEvent(x.Id));
    }

    public override async Task FindAndUpdate(Expression<Func<T, bool>> filterExpression, UpdateDefinition<T> update)
    {
        await base.FindAndUpdate(filterExpression, update);
        DataUpdateEvent(GetSingle(filterExpression.Compile()).Id);
    }

    public override async Task Replace(T item)
    {
        await base.Replace(item);
        DataUpdateEvent(item.Id);
    }

    public override async Task Delete(string id)
    {
        await base.Delete(id);
        DataDeleteEvent(id);
    }

    public override async Task Delete(T item)
    {
        await base.Delete(item);
        DataDeleteEvent(item.Id);
    }

    public override async Task DeleteMany(Expression<Func<T, bool>> filterExpression)
    {
        await base.DeleteMany(filterExpression);
        Get(filterExpression.Compile()).ForEach(x => DataDeleteEvent(x.Id));
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

    protected virtual void DataEvent(EventModel dataModel)
    {
        _eventBus.Send(dataModel);
    }
}
