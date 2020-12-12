using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using UKSF.Api.Base.Models;
using SortDirection = UKSF.Api.Base.Models.SortDirection;

namespace UKSF.Api.Base.Context {
    public abstract class MongoContextBase<T> where T : MongoObject {
        private readonly IMongoCollection<T> _mongoCollection;

        protected MongoContextBase(IMongoCollectionFactory mongoCollectionFactory, string collectionName) => _mongoCollection = mongoCollectionFactory.CreateMongoCollection<T>(collectionName);

        public virtual IEnumerable<T> Get() => _mongoCollection.Get();

        public virtual IEnumerable<T> Get(Func<T, bool> predicate) => _mongoCollection.Get(predicate);

        public virtual PagedResult<T> GetPaged(int page, int pageSize, SortDirection sortDirection, string sortField, IEnumerable<Expression<Func<T, object>>> filterPropertSelectors, string filter) {
            SortDefinition<T> sortDefinition = sortDirection == SortDirection.ASCENDING ? Builders<T>.Sort.Ascending(sortField) : Builders<T>.Sort.Descending(sortField);
            FilterDefinition<T> filterDefinition = string.IsNullOrEmpty(filter)
                ? Builders<T>.Filter.Empty
                : Builders<T>.Filter.Or(filterPropertSelectors.Select(x => Builders<T>.Filter.Regex(x, new BsonRegularExpression(new Regex(filter, RegexOptions.IgnoreCase)))));
            return _mongoCollection.GetPaged(page, pageSize, sortDefinition, filterDefinition);
        }

        public virtual T GetSingle(string id) {
            ValidateId(id);
            return _mongoCollection.GetSingle(id);
        }

        public virtual T GetSingle(Func<T, bool> predicate) => _mongoCollection.GetSingle(predicate);

        public virtual async Task Add(T item) {
            if (item == null) throw new ArgumentNullException(nameof(item));
            await _mongoCollection.AddAsync(item);
        }

        public virtual async Task Update(string id, Expression<Func<T, object>> fieldSelector, object value) {
            ValidateId(id);
            UpdateDefinition<T> update = value == null ? Builders<T>.Update.Unset(fieldSelector) : Builders<T>.Update.Set(fieldSelector, value);
            await _mongoCollection.UpdateAsync(id, update);
        }

        public virtual async Task Update(string id, UpdateDefinition<T> update) {
            ValidateId(id);
            await _mongoCollection.UpdateAsync(id, update);
        }

        public virtual async Task Update(Expression<Func<T, bool>> filterExpression, UpdateDefinition<T> update) {
            await _mongoCollection.UpdateAsync(Builders<T>.Filter.Where(filterExpression), update);
        }

        public virtual async Task UpdateMany(Expression<Func<T, bool>> filterExpression, UpdateDefinition<T> update) {
            await _mongoCollection.UpdateManyAsync(filterExpression, update);
        }

        public virtual async Task Replace(T item) {
            await _mongoCollection.ReplaceAsync(item.Id, item);
        }

        public virtual async Task Delete(string id) {
            ValidateId(id);
            await _mongoCollection.DeleteAsync(id);
        }

        public virtual async Task Delete(T item) {
            await _mongoCollection.DeleteAsync(item.Id);
        }

        public virtual async Task DeleteMany(Expression<Func<T, bool>> filterExpression) {
            await _mongoCollection.DeleteManyAsync(filterExpression);
        }

        private static void ValidateId(string id) {
            if (string.IsNullOrEmpty(id)) throw new KeyNotFoundException("Id cannot be empty");
            if (!ObjectId.TryParse(id, out ObjectId _)) throw new KeyNotFoundException("Id must be a valid ObjectId");
        }
    }
}
