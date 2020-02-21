using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MongoDB.Driver;

namespace UKSF.Api.Interfaces.Data {
    public interface IDataCollection {
        void SetCollectionName(string newCollectionName);
        void AssertCollectionExists<T>();
        List<T> Get<T>();
        List<T> Get<T>(Func<T, bool> predicate);
        T GetSingle<T>(string id);
        T GetSingle<T>(Func<T, bool> predicate);
        Task Add<T>(T data);
        Task Add<T>(string collection, T data);
        Task Update<T>(string id, UpdateDefinition<T> update);
        Task UpdateMany<T>(Expression<Func<T, bool>> predicate, UpdateDefinition<T> update);
        Task Replace<T>(string id, T value);
        Task Delete<T>(string id);
        Task DeleteMany<T>(Expression<Func<T, bool>> predicate);
    }
}
