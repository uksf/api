using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSFWebsite.Api.Interfaces.Data;
using UKSFWebsite.Api.Models.Events;

namespace UKSFWebsite.Api.Services.Fake {
    public abstract class FakeDataService<T> : IDataService<T> {
        public List<T> Get() => new List<T>();

        public List<T> Get(Func<T, bool> predicate) => new List<T>();

        public T GetSingle(string id) => default;

        public T GetSingle(Func<T, bool> predicate) => default;

        public Task Add(T data) => Task.CompletedTask;

        public Task Update(string id, string fieldName, object value) => Task.CompletedTask;

        public Task Update(string id, UpdateDefinition<T> update) => Task.CompletedTask;

        public Task Delete(string id) => Task.CompletedTask;

        public IObservable<DataEventModel> EventBus() => new Subject<DataEventModel>();
    }
}
