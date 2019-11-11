using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSFWebsite.Api.Interfaces.Data;

namespace UKSFWebsite.Api.Data {
    public abstract class DataService<T> : IDataService<T> {
        protected readonly IMongoDatabase Database;
        protected readonly string DatabaseCollection;

        protected DataService(IMongoDatabase database, string collectionName) {
            Database = database;
            DatabaseCollection = collectionName;
            if (Database.GetCollection<T>(DatabaseCollection) == null) {
                Database.CreateCollection(DatabaseCollection);
            }
        }

        public virtual List<T> Get() => Database.GetCollection<T>(DatabaseCollection).AsQueryable().ToList();

        public virtual List<T> Get(Func<T, bool> predicate) => Database.GetCollection<T>(DatabaseCollection).AsQueryable().Where(predicate).ToList();

        public virtual T GetSingle(string id) {
            return Database.GetCollection<T>(DatabaseCollection).AsQueryable().ToList().FirstOrDefault(x => GetIdValue(x) == id);
        }

        public virtual T GetSingle(Func<T, bool> predicate) => Database.GetCollection<T>(DatabaseCollection).AsQueryable().ToList().FirstOrDefault(predicate);

        public virtual async Task Add(T data) {
            await Database.GetCollection<T>(DatabaseCollection).InsertOneAsync(data);
        }

        public virtual async Task Update(string id, string fieldName, object value) {
            UpdateDefinition<T> update = value == null ? Builders<T>.Update.Unset(fieldName) : Builders<T>.Update.Set(fieldName, value);
            await Database.GetCollection<T>(DatabaseCollection).UpdateOneAsync(Builders<T>.Filter.Eq("id", id), update);
        }

        public virtual async Task Update(string id, UpdateDefinition<T> update) {
            await Database.GetCollection<T>(DatabaseCollection).UpdateOneAsync(Builders<T>.Filter.Eq("id", id), update);
        }

        public virtual async Task Delete(string id) {
            await Database.GetCollection<T>(DatabaseCollection).DeleteOneAsync(Builders<T>.Filter.Eq("id", id));
        }

        internal static string GetIdValue(T data) => data.GetType().GetField("id").GetValue(data) as string;
    }
}
