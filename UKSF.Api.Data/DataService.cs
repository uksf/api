using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.Events.Data;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Events;

namespace UKSF.Api.Data {
    public abstract class DataService<T, TData> : DataEventBacker<TData>, IDataService<T, TData> {
        private readonly IDataCollection dataCollection;

        protected DataService(IDataCollection dataCollection, IDataEventBus<TData> dataEventBus, string collectionName) : base(dataEventBus) {
            this.dataCollection = dataCollection;

            dataCollection.SetCollectionName(collectionName);
            dataCollection.AssertCollectionExists<T>();
        }

        public virtual List<T> Get() => dataCollection.Get<T>();

        public virtual List<T> Get(Func<T, bool> predicate) => dataCollection.Get(predicate);

        public virtual T GetSingle(string id) => dataCollection.GetSingle<T>(id);

        public virtual T GetSingle(Func<T, bool> predicate) => dataCollection.GetSingle(predicate);

        public virtual async Task Add(T data) {
            await dataCollection.Add(data);
            DataEvent(EventModelFactory.CreateDataEvent<TData>(DataEventType.ADD, data.GetIdValue(), data));
        }

        public virtual async Task Update(string id, string fieldName, object value) {
            UpdateDefinition<T> update = value == null ? Builders<T>.Update.Unset(fieldName) : Builders<T>.Update.Set(fieldName, value);
            await dataCollection.Update(id, update);
            DataEvent(EventModelFactory.CreateDataEvent<TData>(DataEventType.UPDATE, id));
        }

        public virtual async Task Update(string id, UpdateDefinition<T> update) { // TODO: Remove strong typing to UpdateDefinition
            await dataCollection.Update(id, update);
            DataEvent(EventModelFactory.CreateDataEvent<TData>(DataEventType.UPDATE, id));
        }

        public virtual async Task Delete(string id) {
            await dataCollection.Delete<T>(id);
            DataEvent(EventModelFactory.CreateDataEvent<TData>(DataEventType.DELETE, id));
        }
    }
}
