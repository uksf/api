using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using UKSF.Api.Events.Data;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Events;
using UKSF.Common;

namespace UKSF.Api.Data {
    public abstract class DataServiceBase<T, TData> : DataEventBacker<TData> {
        private readonly IDataCollection<T> dataCollection;

        protected DataServiceBase(IDataCollectionFactory dataCollectionFactory, IDataEventBus<TData> dataEventBus, string collectionName) : base(dataEventBus) =>
            dataCollection = dataCollectionFactory.CreateDataCollection<T>(collectionName);

        public virtual IEnumerable<T> Get() => dataCollection.Get();

        public virtual IEnumerable<T> Get(Func<T, bool> predicate) => dataCollection.Get(predicate);

        public virtual T GetSingle(string id) {
            ValidateId(id);
            return dataCollection.GetSingle(id);
        }

        public virtual T GetSingle(Func<T, bool> predicate) => dataCollection.GetSingle(predicate);

        public virtual async Task Add(T data) {
            if (data == null) throw new ArgumentNullException(nameof(data));
            await dataCollection.AddAsync(data);
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

        public virtual async Task UpdateMany(Func<T, bool> predicate, UpdateDefinition<T> update) {
            // List<T> items = Get(predicate); // TODO: Evaluate performance impact of this presence check
            // if (items.Count == 0) return; // throw new KeyNotFoundException("Could not find any items to update");
            await dataCollection.UpdateManyAsync(x => predicate(x), update);
        }

        public virtual async Task Replace(T item) {
            // if (GetSingle(item.GetIdValue()) == null) throw new KeyNotFoundException("Could not find item to replace"); // TODO: Evaluate performance impact of this presence check
            await dataCollection.ReplaceAsync(item.GetIdValue(), item);
        }

        public virtual async Task Delete(string id) {
            ValidateId(id);
            await dataCollection.DeleteAsync(id);
        }

        public virtual async Task DeleteMany(Func<T, bool> predicate) {
            // List<T> items = Get(predicate); // TODO: Evaluate performance impact of this presence check
            // if (items.Count == 0) return; // throw new KeyNotFoundException("Could not find any items to delete");
            await dataCollection.DeleteManyAsync(x => predicate(x));
        }

        private static void ValidateId(string id) {
            if (string.IsNullOrEmpty(id)) throw new KeyNotFoundException("Key cannot be empty");
            if (!ObjectId.TryParse(id, out ObjectId _)) throw new KeyNotFoundException("Key must be valid");
        }
    }
}
