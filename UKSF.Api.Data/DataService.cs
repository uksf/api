using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.Events;
using UKSF.Api.Events.Data;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Events;
using UKSF.Common;

namespace UKSF.Api.Data {
    public abstract class DataService<T, TData> : DataEventBacker<TData>, IDataService<T, TData> {
        private readonly IDataCollection dataCollection;

        protected DataService(IDataCollection dataCollection, IDataEventBus<TData> dataEventBus, string collectionName) : base(dataEventBus) {
            this.dataCollection = dataCollection;
            SetCollectionName(collectionName);
        }

        public virtual List<T> Get() => dataCollection.Get<T>();

        public virtual List<T> Get(Func<T, bool> predicate) => dataCollection.Get(predicate);

        public virtual T GetSingle(string id) => dataCollection.GetSingle<T>(id);

        public virtual T GetSingle(Func<T, bool> predicate) => dataCollection.GetSingle(predicate);

        public virtual async Task Add(T data) {
            if (data == null) throw new ArgumentNullException(nameof(data));
            await dataCollection.Add(data);
            DataEvent(EventModelFactory.CreateDataEvent<TData>(DataEventType.ADD, data.GetIdValue(), data));
        }

        public virtual async Task Update(string id, string fieldName, object value) {
            if (string.IsNullOrEmpty(id)) throw new KeyNotFoundException("Key cannot be empty");
            UpdateDefinition<T> update = value == null ? Builders<T>.Update.Unset(fieldName) : Builders<T>.Update.Set(fieldName, value);
            await dataCollection.Update(id, update);
            DataEvent(EventModelFactory.CreateDataEvent<TData>(DataEventType.UPDATE, id));
        }

        public virtual async Task Update(string id, UpdateDefinition<T> update) { // TODO: Remove strong typing to UpdateDefinition
            if (string.IsNullOrEmpty(id)) throw new KeyNotFoundException("Key cannot be empty");
            await dataCollection.Update(id, update);
            DataEvent(EventModelFactory.CreateDataEvent<TData>(DataEventType.UPDATE, id));
        }

        public virtual async Task Delete(string id) {
            if (string.IsNullOrEmpty(id)) throw new KeyNotFoundException("Key cannot be empty");
            await dataCollection.Delete<T>(id);
            DataEvent(EventModelFactory.CreateDataEvent<TData>(DataEventType.DELETE, id));
        }

        public void SetCollectionName(string collectionName) {
            dataCollection.SetCollectionName(collectionName);
            dataCollection.AssertCollectionExists<T>();
        }
    }
}
