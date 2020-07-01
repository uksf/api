using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Models.Events;

namespace UKSF.Api.Services.Fake {
    public abstract class FakeDataService<T, TData> : IDataService<T, TData> {
        public List<T> Get() => new List<T>();

        public List<T> Get(Func<T, bool> predicate) => new List<T>();

        public T GetSingle(string id) => default;

        public T GetSingle(Func<T, bool> predicate) => default;

        public Task Add(T data) => Task.CompletedTask;

        public Task Update(string id, string fieldName, object value) => Task.CompletedTask;

        public Task Update(string id, UpdateDefinition<T> update) => Task.CompletedTask;

        public Task Update(Expression<Func<T, bool>> filterExpression, UpdateDefinition<T> update) => Task.CompletedTask;

        public Task UpdateMany(Func<T, bool> predicate, UpdateDefinition<T> update) => Task.CompletedTask;

        public Task Replace(T item) => Task.CompletedTask;

        public Task Delete(string id) => Task.CompletedTask;

        public Task DeleteMany(Func<T, bool> predicate) => Task.CompletedTask;

        public Task SetCollectionNameAsync(string collectionName) => Task.CompletedTask;

        public IObservable<DataEventModel<TData>> EventBus() => new Subject<DataEventModel<TData>>();
    }
}
