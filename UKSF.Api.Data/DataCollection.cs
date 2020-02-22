using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.Interfaces.Data;
using UKSF.Common;

namespace UKSF.Api.Data {
    public class DataCollection : IDataCollection {
        private readonly IMongoDatabase database;
        private string collectionName;

        public DataCollection(IMongoDatabase database) => this.database = database;

        public void SetCollectionName(string newCollectionName) => collectionName = newCollectionName;

        public void AssertCollectionExists<T>() {
            if (GetCollection<T>() == null) {
                database.CreateCollection(collectionName);
            }
        }

        public List<T> Get<T>() => GetCollection<T>().AsQueryable().ToList();

        public List<T> Get<T>(Func<T, bool> predicate) => GetCollection<T>().AsQueryable().Where(predicate).ToList();

        public T GetSingle<T>(string id) {
            return GetCollection<T>().AsQueryable().FirstOrDefault(x => x.GetIdValue() == id); // TODO: Async
        }

        public T GetSingle<T>(Func<T, bool> predicate) => GetCollection<T>().AsQueryable().FirstOrDefault(predicate); // TODO: Async

        public async Task Add<T>(T data) {
            await GetCollection<T>().InsertOneAsync(data);
        }

        public async Task Add<T>(string collection, T data) {
            string oldCollectionName = collectionName;
            SetCollectionName(collection);
            AssertCollectionExists<T>();
            await GetCollection<T>().InsertOneAsync(data);
            SetCollectionName(oldCollectionName);
        }

        public async Task Update<T>(string id, UpdateDefinition<T> update) { // TODO: Remove strong typing of UpdateDefinition as parameter
            await GetCollection<T>().UpdateOneAsync(Builders<T>.Filter.Eq("id", id), update);
        }

        public async Task UpdateMany<T>(Expression<Func<T, bool>> predicate, UpdateDefinition<T> update) { // TODO: Remove strong typing of UpdateDefinition as parameter
            await GetCollection<T>().UpdateManyAsync(predicate, update);
        }

        public async Task Replace<T>(string id, T value) {
            await GetCollection<T>().ReplaceOneAsync(x => x.GetIdValue() == id, value);
        }

        public async Task Delete<T>(string id) {
            await GetCollection<T>().DeleteOneAsync(Builders<T>.Filter.Eq("id", id));
        }

        public async Task DeleteMany<T>(Expression<Func<T, bool>> predicate) {
            await GetCollection<T>().DeleteManyAsync(predicate);
        }

        private IMongoCollection<T> GetCollection<T>() => database.GetCollection<T>(collectionName);
    }
}
