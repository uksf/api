using UKSF.Api.Base.Database;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Models.Logging;
using UKSF.Api.Base.Services.Data;

namespace UKSF.Api.Services.Data {
    public interface ILogDataService : IDataService<BasicLog> { }

    public interface IAuditLogDataService : IDataService<BasicLog> { }

    public interface IHttpErrorLogDataService : IDataService<BasicLog> { }

    public interface ILauncherLogDataService : IDataService<BasicLog> { }

    public class LogDataService : DataService<BasicLog>, ILogDataService {
        public LogDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<BasicLog> dataEventBus) : base(dataCollectionFactory, dataEventBus, "logs") { }
    }

    public class AuditLogDataService : DataService<BasicLog>, IAuditLogDataService {
        public AuditLogDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<BasicLog> dataEventBus) : base(dataCollectionFactory, dataEventBus, "auditLogs") { }
    }

    public class HttpErrorLogDataService : DataService<BasicLog>, IHttpErrorLogDataService {
        public HttpErrorLogDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<BasicLog> dataEventBus) : base(dataCollectionFactory, dataEventBus, "errorLogs") { }
    }

    public class LauncherLogDataService : DataService<BasicLog>, ILauncherLogDataService {
        public LauncherLogDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<BasicLog> dataEventBus) : base(dataCollectionFactory, dataEventBus, "launcherLogs") { }
    }
}
