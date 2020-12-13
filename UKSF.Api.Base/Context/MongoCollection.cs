using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using UKSF.Api.Base.Models;

namespace UKSF.Api.Base.Context {
    public interface IMongoCollection<T> where T : MongoObject {
        IEnumerable<T> Get();
        IEnumerable<T> Get(Func<T, bool> predicate);
        PagedResult<T> GetPaged(int page, int pageSize, SortDefinition<T> sortDefinition, FilterDefinition<T> filterDefinition);
        T GetSingle(string id);
        T GetSingle(Func<T, bool> predicate);
        Task AddAsync(T data);
        Task UpdateAsync(string id, UpdateDefinition<T> update);
        Task UpdateAsync(FilterDefinition<T> filter, UpdateDefinition<T> update);
        Task UpdateManyAsync(Expression<Func<T, bool>> predicate, UpdateDefinition<T> update);
        Task ReplaceAsync(string id, T value);
        Task DeleteAsync(string id);
        Task DeleteManyAsync(Expression<Func<T, bool>> predicate);
    }

    public class MongoCollection<T> : IMongoCollection<T> where T : MongoObject {
        private readonly string _collectionName;
        private readonly IMongoDatabase _database;

        public MongoCollection(IMongoDatabase database, string collectionName) {
            _database = database;
            _collectionName = collectionName;
        }

        public IEnumerable<T> Get() => GetCollection().AsQueryable();

        public IEnumerable<T> Get(Func<T, bool> predicate) => GetCollection().AsQueryable().Where(predicate);

        public PagedResult<T> GetPaged(int page, int pageSize, SortDefinition<T> sortDefinition, FilterDefinition<T> filterDefinition) {
            AggregateFacet<T, AggregateCountResult> countFacet = AggregateFacet.Create(
                "count",
                PipelineDefinition<T, AggregateCountResult>.Create(new[] { PipelineStageDefinitionBuilder.Count<T>() })
            );

            AggregateFacet<T, T> dataFacet = AggregateFacet.Create(
                "data",
                PipelineDefinition<T, T>.Create(
                    new[] { PipelineStageDefinitionBuilder.Sort(sortDefinition), PipelineStageDefinitionBuilder.Skip<T>((page - 1) * pageSize), PipelineStageDefinitionBuilder.Limit<T>(pageSize) }
                )
            );

            IAggregateFluent<AggregateFacetResults> aggregation = GetCollection().Aggregate().Match(filterDefinition).Facet(countFacet, dataFacet);
            IReadOnlyList<AggregateCountResult> aggregateCountResults = aggregation.First().Facets.First(x => x.Name == "count").Output<AggregateCountResult>();
            int count = aggregateCountResults.Count == 0 ? 0 : (int) aggregateCountResults[0].Count;

            IReadOnlyList<T> data = aggregation.First().Facets.First(x => x.Name == "data").Output<T>();

            return new PagedResult<T>(count, data);
        }

        public T GetSingle(string id) => GetCollection().FindSync(Builders<T>.Filter.Eq(x => x.Id, id)).FirstOrDefault();

        public T GetSingle(Func<T, bool> predicate) => GetCollection().AsQueryable().FirstOrDefault(predicate);

        public async Task AddAsync(T data) {
            await GetCollection().InsertOneAsync(data);
        }

        public async Task UpdateAsync(string id, UpdateDefinition<T> update) {
            await GetCollection().UpdateOneAsync(Builders<T>.Filter.Eq(x => x.Id, id), update);
        }

        public async Task UpdateAsync(FilterDefinition<T> filter, UpdateDefinition<T> update) {
            await GetCollection().UpdateOneAsync(filter, update);
        }

        public async Task UpdateManyAsync(Expression<Func<T, bool>> predicate, UpdateDefinition<T> update) {
            // Getting ids by the filter predicate is necessary to cover filtering items by a default model value
            // (e.g Role order default 0, may not be stored in document, and is thus not filterable)
            IEnumerable<string> ids = Get(predicate.Compile()).Select(x => x.Id);
            await GetCollection().UpdateManyAsync(Builders<T>.Filter.In(x => x.Id, ids), update);
        }

        public async Task ReplaceAsync(string id, T value) {
            await GetCollection().ReplaceOneAsync(Builders<T>.Filter.Eq(x => x.Id, id), value);
        }

        public async Task DeleteAsync(string id) {
            await GetCollection().DeleteOneAsync(Builders<T>.Filter.Eq(x => x.Id, id));
        }

        public async Task DeleteManyAsync(Expression<Func<T, bool>> predicate) {
            // This is necessary for filtering items by a default model value (e.g Role order default 0, may not be stored in document)
            IEnumerable<string> ids = Get(predicate.Compile()).Select(x => x.Id);
            await GetCollection().DeleteManyAsync(Builders<T>.Filter.In(x => x.Id, ids));
        }

        public async Task AssertCollectionExistsAsync() {
            if (!await CollectionExistsAsync()) {
                await _database.CreateCollectionAsync(_collectionName);
            }
        }

        private MongoDB.Driver.IMongoCollection<T> GetCollection() => _database.GetCollection<T>(_collectionName);

        private async Task<bool> CollectionExistsAsync() => await (await _database.ListCollectionsAsync(new ListCollectionsOptions { Filter = new BsonDocument("name", _collectionName) })).AnyAsync();
    }
}
