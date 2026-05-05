using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using Mongo2Go;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;
using UKSF.Api.Services;
using Xunit;

namespace UKSF.Api.Tests.Services;

public class MigrationGameConfigExportsToGameDataExportsTests : IDisposable
{
    // Legacy ConfigExportStatus enum values (now removed): Pending=0, Running=1, Success=2,
    // FailedTimeout=3, FailedNoOutput=4, FailedTruncated=5, FailedLaunch=6.
    private const int LegacySuccess = 2;
    private const int LegacyFailedTimeout = 3;
    private const int LegacyFailedNoOutput = 4;

    private readonly MongoDbRunner _runner = MongoDbRunner.Start(singleNodeReplSet: false);
    private readonly IMongoDatabase _database;
    private readonly Mock<IMigrationContext> _migrationContextMock = new();
    private readonly Mock<IUksfLogger> _loggerMock = new();
    private readonly MigrationUtility _sut;

    public MigrationGameConfigExportsToGameDataExportsTests()
    {
        _database = new MongoClient(_runner.ConnectionString).GetDatabase("test");
        _sut = new MigrationUtility(_migrationContextMock.Object, _database, _loggerMock.Object);
    }

    public void Dispose()
    {
        _runner.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Renames_Collection_And_Maps_Old_Success_To_PartialSuccess_With_HasConfig_True()
    {
        var oldCollection = _database.GetCollection<BsonDocument>("gameConfigExports");
        await oldCollection.InsertManyAsync(
            [
                new BsonDocument
                {
                    ["_id"] = ObjectId.GenerateNewId(),
                    ["modpackVersion"] = "5.23.7",
                    ["gameVersion"] = "2.20",
                    ["triggeredAt"] = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc),
                    ["completedAt"] = new DateTime(2024, 1, 1, 10, 5, 0, DateTimeKind.Utc),
                    ["status"] = LegacySuccess,
                    ["filePath"] = @"C:\Server\ConfigExport\config_5.23.7.cpp",
                    ["failureDetail"] = BsonNull.Value
                },
                new BsonDocument
                {
                    ["_id"] = ObjectId.GenerateNewId(),
                    ["modpackVersion"] = "5.23.6",
                    ["gameVersion"] = "2.20",
                    ["triggeredAt"] = new DateTime(2024, 1, 2, 10, 0, 0, DateTimeKind.Utc),
                    ["completedAt"] = new DateTime(2024, 1, 2, 10, 10, 0, DateTimeKind.Utc),
                    ["status"] = LegacyFailedTimeout,
                    ["filePath"] = BsonNull.Value,
                    ["failureDetail"] = "Export did not complete within 600 s."
                }
            ]
        );

        await InvokeMigrateGameConfigExportsToGameDataExports();

        var collections = await _database.ListCollectionNames().ToListAsync();
        collections.Should().NotContain("gameConfigExports");
        collections.Should().Contain("gameDataExports");

        var newCollection = _database.GetCollection<BsonDocument>("gameDataExports");
        var docs = await newCollection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
        docs.Should().HaveCount(2);

        var success = docs.Single(d => d["modpackVersion"].AsString == "5.23.7");
        success["status"].AsInt32.Should().Be((int)GameDataExportStatus.PartialSuccess);
        success["hasConfig"].AsBoolean.Should().BeTrue();
        success["hasCbaSettings"].AsBoolean.Should().BeFalse();
        success["hasCbaSettingsReference"].AsBoolean.Should().BeFalse();
        success.Contains("filePath").Should().BeFalse();

        var failure = docs.Single(d => d["modpackVersion"].AsString == "5.23.6");
        failure["status"].AsInt32.Should().Be(LegacyFailedTimeout, "non-success status values are passed through unchanged");
        failure["hasConfig"].AsBoolean.Should().BeFalse();
        failure["hasCbaSettings"].AsBoolean.Should().BeFalse();
        failure["hasCbaSettingsReference"].AsBoolean.Should().BeFalse();
        failure.Contains("filePath").Should().BeFalse();
    }

    [Fact]
    public async Task Maps_Legacy_String_Success_Status_To_PartialSuccess()
    {
        // Belt-and-braces: if any historical doc has status as a string ("Success"),
        // the migration must still recognise it.
        var oldCollection = _database.GetCollection<BsonDocument>("gameConfigExports");
        await oldCollection.InsertOneAsync(
            new BsonDocument
            {
                ["_id"] = ObjectId.GenerateNewId(),
                ["modpackVersion"] = "5.23.5",
                ["status"] = "Success",
                ["filePath"] = "legacy.cpp"
            }
        );

        await InvokeMigrateGameConfigExportsToGameDataExports();

        var newCollection = _database.GetCollection<BsonDocument>("gameDataExports");
        var doc = (await newCollection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync()).Single();
        doc["status"].AsInt32.Should().Be((int)GameDataExportStatus.PartialSuccess);
        doc["hasConfig"].AsBoolean.Should().BeTrue();
        doc.Contains("filePath").Should().BeFalse();
    }

    [Fact]
    public async Task Skips_When_Source_Collection_Absent()
    {
        await InvokeMigrateGameConfigExportsToGameDataExports();

        var collections = await _database.ListCollectionNames().ToListAsync();
        collections.Should().NotContain("gameConfigExports");
        collections.Should().NotContain("gameDataExports");
    }

    [Fact]
    public async Task Skips_When_Target_Collection_Already_Exists()
    {
        var oldCollection = _database.GetCollection<BsonDocument>("gameConfigExports");
        await oldCollection.InsertOneAsync(
            new BsonDocument
            {
                ["_id"] = ObjectId.GenerateNewId(),
                ["modpackVersion"] = "5.23.7",
                ["status"] = LegacySuccess,
                ["filePath"] = "old.cpp"
            }
        );

        var existingNew = _database.GetCollection<BsonDocument>("gameDataExports");
        await existingNew.InsertOneAsync(
            new BsonDocument
            {
                ["_id"] = ObjectId.GenerateNewId(),
                ["modpackVersion"] = "9.9.9",
                ["status"] = (int)GameDataExportStatus.Success,
                ["hasConfig"] = true,
                ["hasCbaSettings"] = true,
                ["hasCbaSettingsReference"] = true
            }
        );

        await InvokeMigrateGameConfigExportsToGameDataExports();

        var newCollection = _database.GetCollection<BsonDocument>("gameDataExports");
        var docs = await newCollection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
        docs.Should().HaveCount(1);
        docs[0]["modpackVersion"].AsString.Should().Be("9.9.9");

        var collections = await _database.ListCollectionNames().ToListAsync();
        collections.Should().Contain("gameConfigExports", "untouched when target already populated");
    }

    [Fact]
    public async Task Drops_Source_Collection_Even_When_Empty()
    {
        _database.CreateCollection("gameConfigExports");

        await InvokeMigrateGameConfigExportsToGameDataExports();

        var collections = await _database.ListCollectionNames().ToListAsync();
        collections.Should().NotContain("gameConfigExports");
        collections.Should().NotContain("gameDataExports", "no documents to migrate, no need to materialise target");
    }

    [Fact]
    public async Task Does_Not_Default_Status_When_Field_Absent()
    {
        var oldCollection = _database.GetCollection<BsonDocument>("gameConfigExports");
        await oldCollection.InsertOneAsync(
            new BsonDocument
            {
                ["_id"] = ObjectId.GenerateNewId(),
                ["modpackVersion"] = "5.0.0",
                ["filePath"] = "old/path"
            }
        );

        await InvokeMigrateGameConfigExportsToGameDataExports();

        var newCollection = _database.GetCollection<BsonDocument>("gameDataExports");
        var docs = await newCollection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
        docs.Should().HaveCount(1);

        var doc = docs.Single();
        doc["hasConfig"].AsBoolean.Should().BeFalse();
        doc["hasCbaSettings"].AsBoolean.Should().BeFalse();
        doc["hasCbaSettingsReference"].AsBoolean.Should().BeFalse();
        doc.Contains("filePath").Should().BeFalse();
        doc.Contains("status").Should().BeFalse("status field must not be invented when missing from source");
    }

    [Fact]
    public async Task Failed_NoOutput_Status_Is_Preserved_With_HasConfig_False()
    {
        // Mirrors the real devLocal failure case: legacy status int 4 (FailedNoOutput),
        // no filePath, must end up with hasConfig=false and status passed through.
        var oldCollection = _database.GetCollection<BsonDocument>("gameConfigExports");
        await oldCollection.InsertOneAsync(
            new BsonDocument
            {
                ["_id"] = ObjectId.GenerateNewId(),
                ["modpackVersion"] = "5.23.9",
                ["gameVersion"] = "unknown",
                ["status"] = LegacyFailedNoOutput,
                ["failureDetail"] = "Process exited before output file appeared."
            }
        );

        await InvokeMigrateGameConfigExportsToGameDataExports();

        var newCollection = _database.GetCollection<BsonDocument>("gameDataExports");
        var doc = (await newCollection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync()).Single();
        doc["status"].AsInt32.Should().Be(LegacyFailedNoOutput);
        doc["hasConfig"].AsBoolean.Should().BeFalse();
        doc["hasCbaSettings"].AsBoolean.Should().BeFalse();
        doc["hasCbaSettingsReference"].AsBoolean.Should().BeFalse();
    }

    [Fact]
    public async Task Is_Idempotent_When_Run_Twice()
    {
        var oldCollection = _database.GetCollection<BsonDocument>("gameConfigExports");
        await oldCollection.InsertOneAsync(
            new BsonDocument
            {
                ["_id"] = ObjectId.GenerateNewId(),
                ["modpackVersion"] = "5.23.7",
                ["status"] = LegacySuccess,
                ["filePath"] = "x.cpp"
            }
        );

        await InvokeMigrateGameConfigExportsToGameDataExports();
        await InvokeMigrateGameConfigExportsToGameDataExports();

        var docs = await _database.GetCollection<BsonDocument>("gameDataExports").Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
        docs.Should().HaveCount(1);
    }

    private async Task InvokeMigrateGameConfigExportsToGameDataExports()
    {
        var method = typeof(MigrationUtility).GetMethod("MigrateGameConfigExportsToGameDataExports", BindingFlags.NonPublic | BindingFlags.Instance);
        method.Should().NotBeNull("the migration step must be implemented");
        await (Task)method!.Invoke(_sut, [])!;
    }
}
