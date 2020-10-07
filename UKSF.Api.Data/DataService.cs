using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MongoDB.Driver;
using MoreLinq;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models;
using UKSF.Api.Models.Events;

namespace UKSF.Api.Data {
    public abstract class DataService<T> : DataServiceBase<T>, IDataService<T> where T : DatabaseObject {
        private readonly IDataEventBus<T> dataEventBus;

        protected DataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<T> dataEventBus, string collectionName) : base(dataCollectionFactory, collectionName) =>
            this.dataEventBus = dataEventBus;

        public override async Task Add(T item) {
            await base.Add(item);
            DataAddEvent(item.id, item);
        }

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
            DataUpdateEvent(GetSingle(filterExpression.Compile()).id);
        }

        public override async Task UpdateMany(Expression<Func<T, bool>> filterExpression, UpdateDefinition<T> update) {
            await base.UpdateMany(filterExpression, update);
            Get(filterExpression.Compile()).ForEach(x => DataUpdateEvent(x.id));
        }

        public override async Task Replace(T item) {
            await base.Replace(item);
            DataUpdateEvent(item.id);
        }

        public override async Task Delete(string id) {
            await base.Delete(id);
            DataDeleteEvent(id);
        }

        public override async Task Delete(T item) {
            await base.Delete(item);
            DataDeleteEvent(item.id);
        }

        public override async Task DeleteMany(Expression<Func<T, bool>> filterExpression) {
            await base.DeleteMany(filterExpression);
            Get(filterExpression.Compile()).ForEach(x => DataDeleteEvent(x.id));
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
