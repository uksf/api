using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Services;

public class MigrationUtility(IMigrationContext migrationContext, IServiceProvider serviceProvider, IUksfLogger logger)
{
    private const int Version = 3;

    public async Task RunMigrations()
    {
        if (migrationContext.GetSingle(x => x.Version == Version) is not null)
        {
            return;
        }

        try
        {
            await ExecuteMigrations();
            await migrationContext.Add(new Migration { Version = Version });
            logger.LogInfo($"Migration version {Version} executed successfully");
        }
        catch (Exception e)
        {
            logger.LogError(e);
            throw;
        }
    }

    private async Task ExecuteMigrations()
    {
        await RemoveUnitRolesData();
        await RemoveRoleTypeProperty();
        await UpdateCommandRequestTypes();
        logger.LogInfo("All migrations completed successfully");
    }

    private async Task RemoveUnitRolesData()
    {
        logger.LogInfo("Starting removal of unit roles data");

        var unitsCollection = serviceProvider.GetRequiredService<IMongoCollectionFactory>().CreateMongoCollection<OldDomainUnit>("units");

        // Remove the Roles field from all units using the IMongoCollection interface
        var update = Builders<OldDomainUnit>.Update.Unset("roles");
        await unitsCollection.UpdateManyAsync(x => true, update); // Update all documents

        logger.LogInfo("Removed Roles field from all units");
    }

    private async Task RemoveRoleTypeProperty()
    {
        logger.LogInfo("Starting removal of RoleType property from roles");

        var rolesCollection = serviceProvider.GetRequiredService<IMongoCollectionFactory>().CreateMongoCollection<DomainRole>("roles");

        // Remove the RoleType field from all roles using the IMongoCollection interface
        var update = Builders<DomainRole>.Update.Unset("roleType").Unset("order");
        await rolesCollection.UpdateManyAsync(x => true, update); // Update all documents

        logger.LogInfo("Removed RoleType property from all roles");
    }

    private async Task UpdateCommandRequestTypes()
    {
        logger.LogInfo("Starting command request type migrations");

        var commandRequestsCollection =
            serviceProvider.GetRequiredService<IMongoCollectionFactory>().CreateMongoCollection<DomainCommandRequest>("commandRequests");
        var commandRequestsArchiveCollection = serviceProvider.GetRequiredService<IMongoCollectionFactory>()
                                                              .CreateMongoCollection<DomainCommandRequest>("commandRequestsArchive");

        // Update UnitRole to ChainOfCommandPosition
        var unitRoleUpdate = Builders<DomainCommandRequest>.Update.Set(x => x.Type, CommandRequestType.ChainOfCommandPosition);

        await commandRequestsCollection.UpdateManyAsync(x => x.Type == "Unit Role", unitRoleUpdate);
        await commandRequestsArchiveCollection.UpdateManyAsync(x => x.Type == "Unit Role", unitRoleUpdate);

        logger.LogInfo("Updated command requests from UnitRole to ChainOfCommandPosition");
        logger.LogInfo("Updated archived command requests from UnitRole to ChainOfCommandPosition");

        // Update IndividualRole to Role
        var individualRoleUpdate = Builders<DomainCommandRequest>.Update.Set(x => x.Type, CommandRequestType.Role);

        await commandRequestsCollection.UpdateManyAsync(x => x.Type == "Individual Role", individualRoleUpdate);
        await commandRequestsArchiveCollection.UpdateManyAsync(x => x.Type == "Individual Role", individualRoleUpdate);

        logger.LogInfo("Updated command requests from IndividualRole to Role");
        logger.LogInfo("Updated archived command requests from IndividualRole to Role");

        logger.LogInfo("Command request type migrations completed successfully");
    }
}

public class OldDomainUnit : MongoObject
{
    public UnitBranch Branch { get; set; } = UnitBranch.Combat;
    public string Callsign { get; set; }
    public ChainOfCommand ChainOfCommand { get; set; } = new();

    [BsonIgnore]
    public List<DomainUnit> Children { get; set; }

    public string DiscordRoleId { get; set; }
    public string Icon { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public List<string> Members { get; set; } = new();

    public string Name { get; set; }
    public int Order { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string Parent { get; set; }

    public bool PreferShortname { get; set; }
    public string Shortname { get; set; }
    public string TeamspeakGroup { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public Dictionary<string, string> Roles { get; set; } = new();
}
