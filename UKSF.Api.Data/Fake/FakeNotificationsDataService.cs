using System;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Models.Message;
using UKSF.Api.Services.Fake;

namespace UKSF.Api.Data.Fake {
    public class FakeNotificationsDataService : FakeCachedDataService<Notification, INotificationsDataService>, INotificationsDataService { }
}
