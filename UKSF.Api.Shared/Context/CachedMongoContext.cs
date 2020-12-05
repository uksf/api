using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MongoDB.Driver;
using MoreLinq;
using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Models;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Shared.Context {
    public interface ICachedMongoContext {
        void Refresh();
    }

    public class CachedMongoContext<T> : MongoContextBase<T>, IMongoContext<T>, ICachedMongoContext where T : MongoObject {
        private readonly IEventBus _eventBus;
        protected readonly object LockObject = new();

        protected CachedMongoContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, string collectionName) : base(mongoCollectionFactory, collectionName) => _eventBus = eventBus;

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
            return Cache.FirstOrDefault(x => x.Id == id);
        }

        public override T GetSingle(Func<T, bool> predicate) {
            if (Cache == null) Get();
            return Cache.FirstOrDefault(predicate);
        }

        public override async Task Add(T item) {
            if (Cache == null) Get();
            await base.Add(item);
            SetCache(Cache.Concat(new[] { item }));
            DataAddEvent(item);
        }

        public override async Task Update(string id, Expression<Func<T, object>> fieldSelector, object value) {
            await base.Update(id, fieldSelector, value);
            Refresh(); // TODO: intelligent refresh
            DataUpdateEvent(id);
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
            DataUpdateEvent(GetSingle(filterExpression.Compile()).Id);
        }

        public override async Task UpdateMany(Expression<Func<T, bool>> filterExpression, UpdateDefinition<T> update) {
            await base.UpdateMany(filterExpression, update);
            Refresh(); // TODO: intelligent refresh
            Get(filterExpression.Compile()).ForEach(x => DataUpdateEvent(x.Id));
        }

        public override async Task Replace(T item) {
            string id = item.Id;
            T cacheItem = GetSingle(id);
            await base.Replace(item);
            SetCache(Cache.Except(new[] { cacheItem }).Concat(new[] { item }));
            DataUpdateEvent(item.Id);
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
            DataDeleteEvent(item.Id);
        }

        public override async Task DeleteMany(Expression<Func<T, bool>> filterExpression) {
            List<T> ids = Get(filterExpression.Compile()).ToList();
            await base.DeleteMany(filterExpression);
            SetCache(Cache.Except(ids));
            ids.ForEach(x => DataDeleteEvent(x.Id));
        }

        protected virtual void SetCache(IEnumerable<T> newCollection) {
            lock (LockObject) {
                Cache = newCollection?.ToList();
            }
        }

        private void DataAddEvent(T item) {
            DataEvent(new EventModel(EventType.ADD, new ContextEventData<T>(string.Empty, item)));
        }

        private void DataUpdateEvent(string id) {
            DataEvent(new EventModel(EventType.UPDATE, new ContextEventData<T>(id, null)));
        }

        private void DataDeleteEvent(string id) {
            DataEvent(new EventModel(EventType.DELETE, new ContextEventData<T>(id, null)));
        }

        protected virtual void DataEvent(EventModel eventModel) {
            _eventBus.Send(eventModel);
        }
    }
}
