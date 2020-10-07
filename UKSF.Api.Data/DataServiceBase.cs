using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Models;

namespace UKSF.Api.Data {
    public abstract class DataServiceBase<T> where T : DatabaseObject {
        private readonly IDataCollection<T> dataCollection;

        protected DataServiceBase(IDataCollectionFactory dataCollectionFactory, string collectionName) => dataCollection = dataCollectionFactory.CreateDataCollection<T>(collectionName);

        public virtual IEnumerable<T> Get() => dataCollection.Get();

        public virtual IEnumerable<T> Get(Func<T, bool> predicate) => dataCollection.Get(predicate);

        public virtual T GetSingle(string id) {
            ValidateId(id);
            return dataCollection.GetSingle(id);
        }

        public virtual T GetSingle(Func<T, bool> predicate) => dataCollection.GetSingle(predicate);

        public virtual async Task Add(T item) {
            if (item == null) throw new ArgumentNullException(nameof(item));
            await dataCollection.AddAsync(item);
        }

        public virtual async Task Update(string id, string fieldName, object value) {
            ValidateId(id);
            UpdateDefinition<T> update = value == null ? Builders<T>.Update.Unset(fieldName) : Builders<T>.Update.Set(fieldName, value);
            await dataCollection.UpdateAsync(id, update);
        }

        public virtual async Task Update(string id, UpdateDefinition<T> update) {
            ValidateId(id);
            await dataCollection.UpdateAsync(id, update);
        }

        public virtual async Task Update(Expression<Func<T, bool>> filterExpression, UpdateDefinition<T> update) {
            await dataCollection.UpdateAsync(Builders<T>.Filter.Where(filterExpression), update);
        }

        public virtual async Task UpdateMany(Expression<Func<T, bool>> filterExpression, UpdateDefinition<T> update) {
            await dataCollection.UpdateManyAsync(filterExpression, update);
        }

        public virtual async Task Replace(T item) {
            await dataCollection.ReplaceAsync(item.id, item);
        }

        public virtual async Task Delete(string id) {
            ValidateId(id);
            await dataCollection.DeleteAsync(id);
        }

        public virtual async Task Delete(T item) {
            await dataCollection.DeleteAsync(item.id);
        }

        public virtual async Task DeleteMany(Expression<Func<T, bool>> filterExpression) {
            await dataCollection.DeleteManyAsync(filterExpression);
        }

        private static void ValidateId(string id) {
            if (string.IsNullOrEmpty(id)) throw new KeyNotFoundException("Id cannot be empty");
            if (!ObjectId.TryParse(id, out ObjectId _)) throw new KeyNotFoundException("Id must be valid");
        }
    }
}
