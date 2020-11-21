using UKSF.Api.Base.Context;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Shared.Context {
    public interface ILogContext : IMongoContext<BasicLog> { }

    public interface IAuditLogContext : IMongoContext<AuditLog> { }

    public interface IHttpErrorLogContext : IMongoContext<HttpErrorLog> { }

    public interface ILauncherLogContext : IMongoContext<LauncherLog> { }

    public interface IDiscordLogContext : IMongoContext<DiscordLog> { }

    public class LogContext : MongoContext<BasicLog>, ILogContext {
        public LogContext(IMongoCollectionFactory mongoCollectionFactory, IDataEventBus<BasicLog> dataEventBus) : base(mongoCollectionFactory, dataEventBus, "logs") { }
    }

    public class AuditLogContext : MongoContext<AuditLog>, IAuditLogContext {
        public AuditLogContext(IMongoCollectionFactory mongoCollectionFactory, IDataEventBus<AuditLog> dataEventBus) : base(mongoCollectionFactory, dataEventBus, "auditLogs") { }
    }

    public class HttpErrorLogContext : MongoContext<HttpErrorLog>, IHttpErrorLogContext {
        public HttpErrorLogContext(IMongoCollectionFactory mongoCollectionFactory, IDataEventBus<HttpErrorLog> dataEventBus) : base(mongoCollectionFactory, dataEventBus, "errorLogs") { }
    }

    public class LauncherLogContext : MongoContext<LauncherLog>, ILauncherLogContext {
        public LauncherLogContext(IMongoCollectionFactory mongoCollectionFactory, IDataEventBus<LauncherLog> dataEventBus) : base(mongoCollectionFactory, dataEventBus, "launcherLogs") { }
    }

    public class DiscordLogContext : MongoContext<DiscordLog>, IDiscordLogContext {
        public DiscordLogContext(IMongoCollectionFactory mongoCollectionFactory, IDataEventBus<DiscordLog> dataEventBus) : base(mongoCollectionFactory, dataEventBus, "discordLogs") { }
    }
}
