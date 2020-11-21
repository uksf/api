// using System;
// using System.Collections.Generic;
// using System.Linq.Expressions;
// using System.Threading.Tasks;
// using MongoDB.Driver;
//
// namespace UKSF.Api.Fake {
//     public abstract class FakeDataService<T> : IDataService<T> where T : DatabaseObject {
//         public IEnumerable<T> Get() => new List<T>();
//
//         public IEnumerable<T> Get(Func<T, bool> predicate) => new List<T>();
//
//         public T GetSingle(string id) => default;
//
//         public T GetSingle(Func<T, bool> predicate) => default;
//
//         public Task Add(T item) => Task.CompletedTask;
//
//         public Task Update(string id, string fieldName, object value) => Task.CompletedTask;
//
//         public Task Update(string id, UpdateDefinition<T> update) => Task.CompletedTask;
//
//         public Task Update(Expression<Func<T, bool>> filterExpression, UpdateDefinition<T> update) => Task.CompletedTask;
//
//         public Task UpdateMany(Expression<Func<T, bool>> filterExpression, UpdateDefinition<T> update) => Task.CompletedTask;
//
//         public Task Replace(T item) => Task.CompletedTask;
//
//         public Task Delete(string id) => Task.CompletedTask;
//
//         public Task Delete(T item) => Task.CompletedTask;
//
//         public Task DeleteMany(Expression<Func<T, bool>> filterExpression) => Task.CompletedTask;
//     }
// }


