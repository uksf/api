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
    public class DataCollection : IDataCollection {
        private readonly IMongoDatabase database;
        private readonly string collectionName;

        public DataCollection(IMongoDatabase database, string collectionName) {
            this.database = database;
            this.collectionName = collectionName;
        }

        public async Task AssertCollectionExistsAsync<T>() {
            if (await CollectionExistsAsync()) {
                await database.CreateCollectionAsync(collectionName);
            }
        }

        public List<T> Get<T>() => GetCollection<T>().AsQueryable().ToList();

        public List<T> Get<T>(Func<T, bool> predicate) => GetCollection<T>().AsQueryable().Where(predicate).ToList();

        public T GetSingle<T>(string id) {
            return GetCollection<T>().AsQueryable().FirstOrDefault(x => x.GetIdValue() == id); // TODO: Async
        }

        public T GetSingle<T>(Func<T, bool> predicate) => GetCollection<T>().AsQueryable().FirstOrDefault(predicate); // TODO: Async

        public async Task AddAsync<T>(T data) {
            await GetCollection<T>().InsertOneAsync(data);
        }

        public async Task UpdateAsync<T>(string id, UpdateDefinition<T> update) { // TODO: Remove strong typing of UpdateDefinition as parameter
            await GetCollection<T>().UpdateOneAsync(Builders<T>.Filter.Eq("id", id), update);
        }

        public async Task UpdateManyAsync<T>(Expression<Func<T, bool>> predicate, UpdateDefinition<T> update) { // TODO: Remove strong typing of UpdateDefinition as parameter
            await GetCollection<T>().UpdateManyAsync(predicate, update);
        }

        public async Task ReplaceAsync<T>(string id, T value) {
            await GetCollection<T>().ReplaceOneAsync(x => x.GetIdValue() == id, value);
        }

        public async Task DeleteAsync<T>(string id) {
            await GetCollection<T>().DeleteOneAsync(Builders<T>.Filter.Eq("id", id));
        }

        public async Task DeleteManyAsync<T>(Expression<Func<T, bool>> predicate) {
            await GetCollection<T>().DeleteManyAsync(predicate);
        }

        private IMongoCollection<T> GetCollection<T>() => database.GetCollection<T>(collectionName);

        private async Task<bool> CollectionExistsAsync() => await (await database.ListCollectionsAsync(new ListCollectionsOptions { Filter = new BsonDocument("name", collectionName) })).AnyAsync();
    }
}
