using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;

namespace UKSF.Api.Core.Context;

public interface ILogContext : IMongoContext<BasicLog> { }

public interface IAuditLogContext : IMongoContext<AuditLog> { }

public interface IErrorLogContext : IMongoContext<ErrorLog> { }

public interface ILauncherLogContext : IMongoContext<LauncherLog> { }

public interface IDiscordLogContext : IMongoContext<DiscordLog> { }

public class LogContext : MongoContext<BasicLog>, ILogContext
{
    public LogContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) : base(mongoCollectionFactory, eventBus, "logs") { }
}

public class AuditLogContext : MongoContext<AuditLog>, IAuditLogContext
{
    public AuditLogContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) : base(mongoCollectionFactory, eventBus, "auditLogs") { }
}

public class ErrorLogContext : MongoContext<ErrorLog>, IErrorLogContext
{
    public ErrorLogContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) : base(mongoCollectionFactory, eventBus, "errorLogs") { }
}

public class LauncherLogContext : MongoContext<LauncherLog>, ILauncherLogContext
{
    public LauncherLogContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) : base(mongoCollectionFactory, eventBus, "launcherLogs") { }
}

public class DiscordLogContext : MongoContext<DiscordLog>, IDiscordLogContext
{
    public DiscordLogContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) : base(mongoCollectionFactory, eventBus, "discordLogs") { }
}
