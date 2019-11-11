using MongoDB.Driver;
using UKSFWebsite.Api.Interfaces.Data;
using UKSFWebsite.Api.Models.Command;

namespace UKSFWebsite.Api.Data.Command {
    public class CommandRequestArchiveDataService : DataService<CommandRequest>, ICommandRequestArchiveDataService {
        public CommandRequestArchiveDataService(IMongoDatabase database) : base(database, "commandRequestsArchive") { }
    }
}
