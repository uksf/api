using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MongoDB.Driver;
using MoreLinq;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models;
using UKSF.Api.Models.Events;

namespace UKSF.Api.Data {
    public class CachedDataService<T> : DataServiceBase<T>, IDataService<T>, ICachedDataService where T : DatabaseObject {
        private readonly IDataEventBus<T> dataEventBus;
        protected readonly object LockObject = new object();

        protected CachedDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<T> dataEventBus, string collectionName) : base(dataCollectionFactory, collectionName) =>
            this.dataEventBus = dataEventBus;

        public List<T> Cache { get; protected set; }

        public void Refresh() {
            SetCache(null);
            Get();
        }

        public sealed override IEnumerable<T> Get() {
            if (Cache != null) {
                return Cache;
            }

            SetCache(base.Get());
            return Cache;
        }

        public override IEnumerable<T> Get(Func<T, bool> predicate) {
            if (Cache == null) Get();
            return Cache.Where(predicate);
        }

        public override T GetSingle(string id) {
            if (Cache == null) Get();
            return Cache.FirstOrDefault(x => x.id == id);
        }

        public override T GetSingle(Func<T, bool> predicate) {
            if (Cache == null) Get();
            return Cache.FirstOrDefault(predicate);
        }

        public override async Task Add(T item) {
            if (Cache == null) Get();
            await base.Add(item);
            SetCache(Cache.Concat(new[] { item }));
            DataAddEvent(item.id, item);
        }

        public override async Task Update(string id, string fieldName, object value) {
            await base.Update(id, fieldName, value);
            Refresh(); // TODO: intelligent refresh
            DataUpdateEvent(id);
        }

        public override async Task Update(string id, UpdateDefinition<T> update) {
            await base.Update(id, update);
            Refresh(); // TODO: intelligent refresh
            DataUpdateEvent(id);
        }

        public override async Task Update(Expression<Func<T, bool>> filterExpression, UpdateDefinition<T> update) {
            await base.Update(filterExpression, update);
            Refresh(); // TODO: intelligent refresh
            DataUpdateEvent(GetSingle(filterExpression.Compile()).id);
        }

        public override async Task UpdateMany(Expression<Func<T, bool>> filterExpression, UpdateDefinition<T> update) {
            await base.UpdateMany(filterExpression, update);
            Refresh(); // TODO: intelligent refresh
            Get(filterExpression.Compile()).ForEach(x => DataUpdateEvent(x.id));
        }

        public override async Task Replace(T item) {
            string id = item.id;
            T cacheItem = GetSingle(id);
            await base.Replace(item);
            SetCache(Cache.Except(new[] { cacheItem }).Concat(new[] { item }));
            DataUpdateEvent(item.id);
        }

        public override async Task Delete(string id) {
            T cacheItem = GetSingle(id);
            await base.Delete(id);
            SetCache(Cache.Except(new[] { cacheItem }));
            DataDeleteEvent(id);
        }

        public override async Task Delete(T item) {
            if (Cache == null) Get();
            await base.Delete(item);
            SetCache(Cache.Except(new[] { item }));
            DataDeleteEvent(item.id);
        }

        public override async Task DeleteMany(Expression<Func<T, bool>> filterExpression) {
            List<T> ids = Get(filterExpression.Compile()).ToList();
            await base.DeleteMany(filterExpression);
            SetCache(Cache.Except(ids));
            ids.ForEach(x => DataDeleteEvent(x.id));
        }

        protected virtual void SetCache(IEnumerable<T> newCollection) {
            lock (LockObject) {
                Cache = newCollection?.ToList();
            }
        }

        private void DataAddEvent(string id, T item) {
            DataEvent(EventModelFactory.CreateDataEvent<T>(DataEventType.ADD, id, item));
        }

        private void DataUpdateEvent(string id) {
            DataEvent(EventModelFactory.CreateDataEvent<T>(DataEventType.UPDATE, id));
        }

        private void DataDeleteEvent(string id) {
            DataEvent(EventModelFactory.CreateDataEvent<T>(DataEventType.DELETE, id));
        }

        protected virtual void DataEvent(DataEventModel<T> dataEvent) {
            dataEventBus.Send(dataEvent);
        }
    }
}
