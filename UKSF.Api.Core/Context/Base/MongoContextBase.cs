using System.Linq.Expressions;
using System.Text.RegularExpressions;
using MongoDB.Driver;
using UKSF.Api.Core.Models;
using SortDirection = UKSF.Api.Core.Models.SortDirection;

namespace UKSF.Api.Core.Context.Base;

public abstract class MongoContextBase<T> where T : MongoObject
{
    private readonly IMongoCollection<T> _mongoCollection;

    protected MongoContextBase(IMongoCollectionFactory mongoCollectionFactory, string collectionName)
    {
        _mongoCollection = mongoCollectionFactory.CreateMongoCollection<T>(collectionName);
    }

    public virtual IEnumerable<T> Get()
    {
        return _mongoCollection.Get();
    }

    public virtual IEnumerable<T> Get(Func<T, bool> predicate)
    {
        return _mongoCollection.Get(predicate);
    }

    public virtual PagedResult<T> GetPaged(
        int page,
        int pageSize,
        SortDirection sortDirection,
        string sortField,
        IEnumerable<Expression<Func<T, object>>> filterPropertSelectors,
        string filter
    )
    {
        var sortDefinition = sortDirection == SortDirection.ASCENDING ? Builders<T>.Sort.Ascending(sortField) : Builders<T>.Sort.Descending(sortField);
        var filterDefinition = string.IsNullOrEmpty(filter)
            ? Builders<T>.Filter.Empty
            : Builders<T>.Filter.Or(filterPropertSelectors.Select(x => Builders<T>.Filter.Regex(x, new(new Regex(filter, RegexOptions.IgnoreCase)))));
        return GetPaged(page, pageSize, collection => collection.Aggregate(), sortDefinition, filterDefinition);
    }

    public virtual PagedResult<TOut> GetPaged<TOut>(
        int page,
        int pageSize,
        Func<MongoDB.Driver.IMongoCollection<T>, IAggregateFluent<TOut>> aggregator,
        SortDefinition<TOut> sortDefinition,
        FilterDefinition<TOut> filterDefinition
    )
    {
        return _mongoCollection.GetPaged(page, pageSize, aggregator, sortDefinition, filterDefinition);
    }

    public virtual T GetSingle(string id)
    {
        return _mongoCollection.GetSingle(id);
    }

    public virtual T GetSingle(Func<T, bool> predicate)
    {
        return _mongoCollection.GetSingle(predicate);
    }

    public virtual async Task Add(T item)
    {
        if (item == null)
        {
            throw new ArgumentNullException(nameof(item));
        }

        await _mongoCollection.AddAsync(item);
    }

    public virtual async Task Update<TField>(string id, Expression<Func<T, TField>> fieldSelector, TField value)
    {
        if (value == null)
        {
            Expression converted = Expression.Convert(fieldSelector.Body, typeof(object));
            var unsetFieldSelector = Expression.Lambda<Func<T, object>>(converted, fieldSelector.Parameters);
            await _mongoCollection.UpdateAsync(id, Builders<T>.Update.Unset(unsetFieldSelector));
        }
        else
        {
            await _mongoCollection.UpdateAsync(id, Builders<T>.Update.Set(fieldSelector, value));
        }
    }

    public virtual async Task Update(string id, UpdateDefinition<T> update)
    {
        await _mongoCollection.UpdateAsync(id, update);
    }

    public virtual async Task Update(Expression<Func<T, bool>> filterExpression, UpdateDefinition<T> update)
    {
        await _mongoCollection.UpdateAsync(Builders<T>.Filter.Where(filterExpression), update);
    }

    public virtual async Task UpdateMany(Expression<Func<T, bool>> filterExpression, UpdateDefinition<T> update)
    {
        await _mongoCollection.UpdateManyAsync(filterExpression, update);
    }

    public virtual async Task FindAndUpdate(Expression<Func<T, bool>> filterExpression, UpdateDefinition<T> update)
    {
        await _mongoCollection.FindAndUpdateAsync(Builders<T>.Filter.Where(filterExpression), update);
    }

    public virtual async Task Replace(T item)
    {
        await _mongoCollection.ReplaceAsync(item.Id, item);
    }

    public virtual async Task Delete(string id)
    {
        await _mongoCollection.DeleteAsync(id);
    }

    public virtual async Task Delete(T item)
    {
        await _mongoCollection.DeleteAsync(item.Id);
    }

    public virtual async Task DeleteMany(Expression<Func<T, bool>> filterExpression)
    {
        await _mongoCollection.DeleteManyAsync(filterExpression);
    }

    public FilterDefinition<TFilter> BuildPagedComplexQuery<TFilter>(string query, Func<string, FilterDefinition<TFilter>> filter)
    {
        if (string.IsNullOrWhiteSpace(query) || !query.Split(new[] { "&&", "||" }, StringSplitOptions.RemoveEmptyEntries).Any())
        {
            return Builders<TFilter>.Filter.Empty;
        }

        var andQueryParts = query.Split("&&", StringSplitOptions.RemoveEmptyEntries);
        var andFilters = andQueryParts.Select(andQueryPart => andQueryPart.Split("||", StringSplitOptions.RemoveEmptyEntries))
                                      .Select(orQueryParts => orQueryParts.Select(filter).ToList())
                                      .Select(orFilters => Builders<TFilter>.Filter.Or(orFilters))
                                      .ToList();
        return Builders<TFilter>.Filter.And(andFilters);
    }
}
