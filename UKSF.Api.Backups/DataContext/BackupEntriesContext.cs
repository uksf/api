using UKSF.Api.Backups.Models;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;

namespace UKSF.Api.Backups.DataContext;

public interface IBackupEntriesContext : IMongoContext<DomainBackupEntry>;

public class BackupEntriesContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus)
    : MongoContext<DomainBackupEntry>(mongoCollectionFactory, eventBus, "backupEntries"), IBackupEntriesContext;
