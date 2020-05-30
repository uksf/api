using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.Events.Data;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Events;
using UKSF.Common;

namespace UKSF.Api.Data {
    public abstract class DataService<T, TData> : DataEventBacker<TData>, IDataService<T, TData> {
        private readonly IDataCollection<T> dataCollection;

        protected DataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<TData> dataEventBus, string collectionName) : base(dataEventBus) => dataCollection = dataCollectionFactory.CreateDataCollection<T>(collectionName);

        public virtual List<T> Get() => dataCollection.Get();

        public virtual List<T> Get(Func<T, bool> predicate) => dataCollection.Get(predicate);

        public virtual T GetSingle(string id) => dataCollection.GetSingle(id);

        public virtual T GetSingle(Func<T, bool> predicate) => dataCollection.GetSingle(predicate);

        public virtual async Task Add(T data) {
            if (data == null) throw new ArgumentNullException(nameof(data));
            await dataCollection.AddAsync(data);
            DataEvent(EventModelFactory.CreateDataEvent<TData>(DataEventType.ADD, data.GetIdValue(), data));
        }

        public virtual async Task Update(string id, string fieldName, object value) {
            if (string.IsNullOrEmpty(id)) throw new KeyNotFoundException("Key cannot be empty");
            UpdateDefinition<T> update = value == null ? Builders<T>.Update.Unset(fieldName) : Builders<T>.Update.Set(fieldName, value);
            await dataCollection.UpdateAsync(id, update);
            DataEvent(EventModelFactory.CreateDataEvent<TData>(DataEventType.UPDATE, id));
        }

        public virtual async Task Update(string id, UpdateDefinition<T> update) { // TODO: Remove strong typing to UpdateDefinition
            if (string.IsNullOrEmpty(id)) throw new KeyNotFoundException("Key cannot be empty");
            await dataCollection.UpdateAsync(id, update);
            DataEvent(EventModelFactory.CreateDataEvent<TData>(DataEventType.UPDATE, id));
        }

        public virtual async Task UpdateMany(Func<T, bool> predicate, UpdateDefinition<T> update) {
            List<T> items = Get(predicate); // TODO: Evaluate performance impact of this presence check
            if (items.Count == 0) return; // throw new KeyNotFoundException("Could not find any items to update");
            await dataCollection.UpdateManyAsync(x => predicate(x), update);
            items.ForEach(x => DataEvent(EventModelFactory.CreateDataEvent<TData>(DataEventType.UPDATE, x.GetIdValue())));
        }

        public virtual async Task Replace(T item) {
            if (GetSingle(item.GetIdValue()) == null) throw new KeyNotFoundException("Could not find item to replace"); // TODO: Evaluate performance impact of this presence check
            await dataCollection.ReplaceAsync(item.GetIdValue(), item);
            DataEvent(EventModelFactory.CreateDataEvent<TData>(DataEventType.UPDATE, item.GetIdValue()));
        }

        public virtual async Task Delete(string id) {
            if (string.IsNullOrEmpty(id)) throw new KeyNotFoundException("Key cannot be empty");
            await dataCollection.DeleteAsync(id);
            DataEvent(EventModelFactory.CreateDataEvent<TData>(DataEventType.DELETE, id));
        }

        public virtual async Task DeleteMany(Func<T, bool> predicate) {
            List<T> items = Get(predicate); // TODO: Evaluate performance impact of this presence check
            if (items.Count == 0) return; // throw new KeyNotFoundException("Could not find any items to delete");
            await dataCollection.DeleteManyAsync(x => predicate(x));
            items.ForEach(x => DataEvent(EventModelFactory.CreateDataEvent<TData>(DataEventType.DELETE, x.GetIdValue())));
        }
    }
}
