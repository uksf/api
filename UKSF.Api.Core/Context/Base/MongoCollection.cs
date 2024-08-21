using System.Linq.Expressions;
using MongoDB.Bson;
using MongoDB.Driver;
using UKSF.Api.Core.Models;

namespace UKSF.Api.Core.Context.Base;

public interface IMongoCollection<T> where T : MongoObject
{
    IEnumerable<T> Get();
    IEnumerable<T> Get(Func<T, bool> predicate);

    PagedResult<TOut> GetPaged<TOut>(
        int page,
        int pageSize,
        Func<MongoDB.Driver.IMongoCollection<T>, IAggregateFluent<TOut>> aggregator,
        SortDefinition<TOut> sortDefinition,
        FilterDefinition<TOut> filterDefinition
    );

    T GetSingle(string id);
    T GetSingle(Func<T, bool> predicate);
    Task AddAsync(T data);
    Task UpdateAsync(string id, UpdateDefinition<T> update);
    Task UpdateAsync(FilterDefinition<T> filter, UpdateDefinition<T> update);
    Task UpdateManyAsync(Expression<Func<T, bool>> predicate, UpdateDefinition<T> update);
    Task FindAndUpdateAsync(FilterDefinition<T> filter, UpdateDefinition<T> update);
    Task ReplaceAsync(string id, T value);
    Task DeleteAsync(string id);
    Task DeleteManyAsync(Expression<Func<T, bool>> predicate);
}

public class MongoCollection<T>(IMongoDatabase database, string collectionName) : IMongoCollection<T> where T : MongoObject
{
    public IEnumerable<T> Get()
    {
        return GetCollection().AsQueryable();
    }

    public IEnumerable<T> Get(Func<T, bool> predicate)
    {
        return GetCollection().AsQueryable().Where(predicate);
    }

    public PagedResult<TOut> GetPaged<TOut>(
        int page,
        int pageSize,
        Func<MongoDB.Driver.IMongoCollection<T>, IAggregateFluent<TOut>> aggregator,
        SortDefinition<TOut> sortDefinition,
        FilterDefinition<TOut> filterDefinition
    )
    {
        var countFacet = AggregateFacet.Create(
            "count",
            PipelineDefinition<TOut, AggregateCountResult>.Create([PipelineStageDefinitionBuilder.Count<TOut>()])
        );

        var dataFacet = AggregateFacet.Create(
            "data",
            PipelineDefinition<TOut, TOut>.Create(
                [
                    PipelineStageDefinitionBuilder.Sort(sortDefinition),
                    PipelineStageDefinitionBuilder.Skip<TOut>((page - 1) * pageSize),
                    PipelineStageDefinitionBuilder.Limit<TOut>(pageSize)
                ]
            )
        );

        var aggregation = aggregator(GetCollection()).Match(filterDefinition).Facet(countFacet, dataFacet);
        var aggregateCountResults = aggregation.First().Facets.First(x => x.Name == "count").Output<AggregateCountResult>();
        var count = (int)(aggregateCountResults.FirstOrDefault()?.Count ?? 0);

        var data = aggregation.First().Facets.First(x => x.Name == "data").Output<TOut>();

        return new PagedResult<TOut>(count, data);
    }

    public T GetSingle(string id)
    {
        // TODO: Make all this async
        return GetCollection().FindSync(Builders<T>.Filter.Eq(x => x.Id, id)).FirstOrDefault();
    }

    public T GetSingle(Func<T, bool> predicate)
    {
        return GetCollection().AsQueryable().FirstOrDefault(predicate);
    }

    public async Task AddAsync(T data)
    {
        await GetCollection().InsertOneAsync(data);
    }

    public async Task UpdateAsync(string id, UpdateDefinition<T> update)
    {
        await GetCollection().UpdateOneAsync(Builders<T>.Filter.Eq(x => x.Id, id), update);
    }

    public async Task UpdateAsync(FilterDefinition<T> filter, UpdateDefinition<T> update)
    {
        await GetCollection().UpdateOneAsync(filter, update);
    }

    public async Task UpdateManyAsync(Expression<Func<T, bool>> predicate, UpdateDefinition<T> update)
    {
        // Getting ids by the filter predicate is necessary to cover filtering items by a default model value
        // (e.g Role order default 0, may not be stored in document, and is thus not filterable)
        var ids = Get(predicate.Compile()).Select(x => x.Id);
        await GetCollection().UpdateManyAsync(Builders<T>.Filter.In(x => x.Id, ids), update);
    }

    public async Task FindAndUpdateAsync(FilterDefinition<T> filter, UpdateDefinition<T> update)
    {
        await GetCollection().FindOneAndUpdateAsync(filter, update);
    }

    public async Task ReplaceAsync(string id, T value)
    {
        await GetCollection().ReplaceOneAsync(Builders<T>.Filter.Eq(x => x.Id, id), value);
    }

    public async Task DeleteAsync(string id)
    {
        await GetCollection().DeleteOneAsync(Builders<T>.Filter.Eq(x => x.Id, id));
    }

    public async Task DeleteManyAsync(Expression<Func<T, bool>> predicate)
    {
        // This is necessary for filtering items by a default model value (e.g Role order default 0, may not be stored in document)
        var ids = Get(predicate.Compile()).Select(x => x.Id);
        await GetCollection().DeleteManyAsync(Builders<T>.Filter.In(x => x.Id, ids));
    }

    public async Task AssertCollectionExistsAsync()
    {
        if (!await CollectionExistsAsync())
        {
            await database.CreateCollectionAsync(collectionName);
        }
    }

    private MongoDB.Driver.IMongoCollection<T> GetCollection()
    {
        return database.GetCollection<T>(collectionName);
    }

    private async Task<bool> CollectionExistsAsync()
    {
        return await (await database.ListCollectionsAsync(new ListCollectionsOptions { Filter = new BsonDocument("name", collectionName) })).AnyAsync();
    }
}
