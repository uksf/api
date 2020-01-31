using System;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.Models.Message;

namespace UKSF.Api.Interfaces.Data.Cached {
    public interface INotificationsDataService : IDataService<Notification, INotificationsDataService> {
        Task UpdateMany(Func<Notification, bool> predicate, UpdateDefinition<Notification> update);
        Task DeleteMany(Func<Notification, bool> predicate);
    }
}
