using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MongoDB.Driver;
using MoreLinq;
using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Models;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Shared.Context {
    public interface IMongoContext<T> {
        IEnumerable<T> Get();
        IEnumerable<T> Get(Func<T, bool> predicate);
        T GetSingle(string id);
        T GetSingle(Func<T, bool> predicate);
        Task Add(T item);
        Task Update(string id, Expression<Func<T, object>> fieldSelector, object value);
        Task Update(string id, string fieldName, object value);
        Task Update(string id, UpdateDefinition<T> update);
        Task Update(Expression<Func<T, bool>> filterExpression, UpdateDefinition<T> update);
        Task UpdateMany(Expression<Func<T, bool>> filterExpression, UpdateDefinition<T> update);
        Task Replace(T item);
        Task Delete(string id);
        Task Delete(T item);
        Task DeleteMany(Expression<Func<T, bool>> filterExpression);
    }

    public class MongoContext<T> : MongoContextBase<T>, IMongoContext<T> where T : MongoObject {
        private readonly IEventBus _eventBus;

        protected MongoContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, string collectionName) : base(mongoCollectionFactory, collectionName) => _eventBus = eventBus;

        public override async Task Add(T item) {
            await base.Add(item);
            DataAddEvent(item);
        }

        public override async Task Update(string id, Expression<Func<T, object>> fieldSelector, object value) {
            await base.Update(id, fieldSelector, value);
            DataUpdateEvent(id);
        }

        // TODO: Deprecate
        public override async Task Update(string id, string fieldName, object value) {
            await base.Update(id, fieldName, value);
            DataUpdateEvent(id);
        }

        public override async Task Update(string id, UpdateDefinition<T> update) {
            await base.Update(id, update);
            DataUpdateEvent(id);
        }

        public override async Task Update(Expression<Func<T, bool>> filterExpression, UpdateDefinition<T> update) {
            await base.Update(filterExpression, update);
            DataUpdateEvent(GetSingle(filterExpression.Compile()).Id);
        }

        public override async Task UpdateMany(Expression<Func<T, bool>> filterExpression, UpdateDefinition<T> update) {
            await base.UpdateMany(filterExpression, update);
            Get(filterExpression.Compile()).ForEach(x => DataUpdateEvent(x.Id));
        }

        public override async Task Replace(T item) {
            await base.Replace(item);
            DataUpdateEvent(item.Id);
        }

        public override async Task Delete(string id) {
            await base.Delete(id);
            DataDeleteEvent(id);
        }

        public override async Task Delete(T item) {
            await base.Delete(item);
            DataDeleteEvent(item.Id);
        }

        public override async Task DeleteMany(Expression<Func<T, bool>> filterExpression) {
            await base.DeleteMany(filterExpression);
            Get(filterExpression.Compile()).ForEach(x => DataDeleteEvent(x.Id));
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

        protected virtual void DataEvent(EventModel dataModel) {
            _eventBus.Send(dataModel);
        }
    }
}
