using UKSF.Api.Base.Database;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Models.Logging;

namespace UKSF.Api.Base.Services.Data {
    public interface ILogDataService : IDataService<BasicLog> { }

    public interface IAuditLogDataService : IDataService<AuditLog> { }

    public interface IHttpErrorLogDataService : IDataService<HttpErrorLog> { }

    public interface ILauncherLogDataService : IDataService<LauncherLog> { }

    public class LogDataService : DataService<BasicLog>, ILogDataService {
        public LogDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<BasicLog> dataEventBus) : base(dataCollectionFactory, dataEventBus, "logs") { }
    }

    public class AuditLogDataService : DataService<AuditLog>, IAuditLogDataService {
        public AuditLogDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<AuditLog> dataEventBus) : base(dataCollectionFactory, dataEventBus, "auditLogs") { }
    }

    public class HttpErrorLogDataService : DataService<HttpErrorLog>, IHttpErrorLogDataService {
        public HttpErrorLogDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<HttpErrorLog> dataEventBus) : base(dataCollectionFactory, dataEventBus, "errorLogs") { }
    }

    public class LauncherLogDataService : DataService<LauncherLog>, ILauncherLogDataService {
        public LauncherLogDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<LauncherLog> dataEventBus) : base(dataCollectionFactory, dataEventBus, "launcherLogs") { }
    }
}
