using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using UKSF.Api.Interfaces.Data;
using UKSF.Common;

namespace UKSF.Api.Data {
    public class DataCollection<T> : IDataCollection<T> {
        private readonly IMongoDatabase database;
        private readonly string collectionName;

        public DataCollection(IMongoDatabase database, string collectionName) {
            this.database = database;
            this.collectionName = collectionName;
        }

        public async Task AssertCollectionExistsAsync() {
            if (!await CollectionExistsAsync()) {
                await database.CreateCollectionAsync(collectionName);
            }
        }

        public IEnumerable<T> Get() => GetCollection().AsQueryable();

        public IEnumerable<T> Get(Func<T, bool> predicate) => GetCollection().AsQueryable().Where(predicate);

        public T GetSingle(string id) => GetCollection().FindSync(Builders<T>.Filter.Eq("id", id)).FirstOrDefault();

        public T GetSingle(Func<T, bool> predicate) => GetCollection().AsQueryable().FirstOrDefault(predicate);

        public async Task AddAsync(T data) {
            await GetCollection().InsertOneAsync(data);
        }

        public async Task UpdateAsync(string id, UpdateDefinition<T> update) { // TODO: Remove strong typing of UpdateDefinition as parameter
            await GetCollection().UpdateOneAsync(Builders<T>.Filter.Eq("id", id), update);
        }

        public async Task UpdateAsync(FilterDefinition<T> filter, UpdateDefinition<T> update) { // TODO: Remove strong typing of UpdateDefinition as parameter
            await GetCollection().UpdateOneAsync(filter, update);
        }

        public async Task UpdateManyAsync(Expression<Func<T, bool>> predicate, UpdateDefinition<T> update) { // TODO: Remove strong typing of UpdateDefinition as parameter
            IEnumerable<string> ids = Get(predicate.Compile()).Select(x => x.GetIdValue()); // This is necessary for filtering items by a default model value (e.g Role order default 0, may not be stored in document)
            await GetCollection().UpdateManyAsync(Builders<T>.Filter.In("id", ids), update);
        }

        public async Task ReplaceAsync(string id, T value) {
            await GetCollection().ReplaceOneAsync(Builders<T>.Filter.Eq("id", id), value);
        }

        public async Task DeleteAsync(string id) {
            await GetCollection().DeleteOneAsync(Builders<T>.Filter.Eq("id", id));
        }

        public async Task DeleteManyAsync(Expression<Func<T, bool>> predicate) {
            IEnumerable<string> ids = Get(predicate.Compile()).Select(x => x.GetIdValue()); // This is necessary for filtering items by a default model value (e.g Role order default 0, may not be stored in document)
            await GetCollection().DeleteManyAsync(Builders<T>.Filter.In("id", ids));
        }

        private IMongoCollection<T> GetCollection() => database.GetCollection<T>(collectionName);

        private async Task<bool> CollectionExistsAsync() => await (await database.ListCollectionsAsync(new ListCollectionsOptions { Filter = new BsonDocument("name", collectionName) })).AnyAsync();
    }
}
