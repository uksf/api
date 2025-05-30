using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Services;

public class MigrationUtility(IMigrationContext migrationContext, IUksfLogger logger, IServiceProvider serviceProvider)
{
    private const int Version = 2;

    public void Migrate()
    {
        if (migrationContext.GetSingle(x => x.Version == Version) is not null)
        {
            return;
        }

        try
        {
            ExecuteMigration();
            migrationContext.Add(new Migration { Version = Version });
            logger.LogInfo($"Migration version {Version} executed successfully");
        }
        catch (Exception e)
        {
            logger.LogError(e);
            throw;
        }
    }

    private void ExecuteMigration()
    {
        MigrateChainOfCommand();
    }

    private void MigrateChainOfCommand()
    {
        logger.LogInfo("Starting chain of command migration");

        var legacyUnitsCollection = serviceProvider.GetRequiredService<IMongoCollectionFactory>().CreateMongoCollection<DomainUnitLegacy>("units");
        var unitsContext = serviceProvider.GetRequiredService<IUnitsContext>();

        var legacyUnits = legacyUnitsCollection.Get().ToList();
        var migratedCount = 0;

        foreach (var legacyUnit in legacyUnits)
        {
            var chainOfCommand = new ChainOfCommand();

            // Migrate from legacy Roles dictionary to ChainOfCommand
            if (legacyUnit.Roles?.Count > 0)
            {
                foreach (var role in legacyUnit.Roles)
                {
                    chainOfCommand.SetMemberAtPosition(role.Key, role.Value);
                }

                migratedCount++;
            }

            // Update the unit with the new ChainOfCommand
            unitsContext.Update(legacyUnit.Id, unit => unit.ChainOfCommand, chainOfCommand).GetAwaiter().GetResult();
        }

        logger.LogInfo($"Chain of command migration completed. Migrated {migratedCount} units with roles.");
    }
}

// Temporary legacy model for migration
public class DomainUnitLegacy : MongoObject
{
    public UnitBranch Branch { get; set; } = UnitBranch.Combat;
    public string Callsign { get; set; }

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

    [BsonRepresentation(BsonType.ObjectId)]
    public Dictionary<string, string> Roles { get; set; } = new();

    public string Shortname { get; set; }
    public string TeamspeakGroup { get; set; }
}
