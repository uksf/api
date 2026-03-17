using FluentAssertions;
using MongoDB.Bson;
using UKSF.Api.ArmaServer.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class PersistenceDataMigrationTests
{
    [Fact]
    public void UnwrapDiscriminators_UnwrapsSimpleObjectArray()
    {
        var input = new BsonDocument { { "_t", "System.Object[]" }, { "_v", new BsonArray { "item1", "item2" } } };

        var result = PersistenceDataMigration.UnwrapDiscriminators(input);

        result.Should().BeOfType<BsonArray>();
        result.AsBsonArray.Should().HaveCount(2);
        result.AsBsonArray[0].AsString.Should().Be("item1");
        result.AsBsonArray[1].AsString.Should().Be("item2");
    }

    [Fact]
    public void UnwrapDiscriminators_UnwrapsSystemObjectVariants()
    {
        var input = new BsonDocument
        {
            { "_t", "System.Object" },
            {
                "_v", new BsonArray
                {
                    1,
                    2,
                    3
                }
            }
        };

        var result = PersistenceDataMigration.UnwrapDiscriminators(input);

        result.Should().BeOfType<BsonArray>();
        result.AsBsonArray.Should().HaveCount(3);
    }

    [Fact]
    public void UnwrapDiscriminators_UnwrapsNestedDiscriminatorsInArray()
    {
        var input = new BsonArray
        {
            new BsonDocument
            {
                { "_t", "System.Object[]" },
                {
                    "_v", new BsonArray
                    {
                        1,
                        2,
                        3
                    }
                }
            }
        };

        var result = PersistenceDataMigration.UnwrapDiscriminators(input);

        result.AsBsonArray[0].Should().BeOfType<BsonArray>();
        result.AsBsonArray[0]
        .AsBsonArray.Should()
        .BeEquivalentTo(
            new BsonArray
            {
                1,
                2,
                3
            }
        );
    }

    [Fact]
    public void UnwrapDiscriminators_PreservesRegularDocuments()
    {
        var input = new BsonDocument { { "key", "test" }, { "objects", new BsonArray() } };

        var result = PersistenceDataMigration.UnwrapDiscriminators(input);

        result.AsBsonDocument["key"].AsString.Should().Be("test");
        result.AsBsonDocument["objects"].AsBsonArray.Should().BeEmpty();
    }

    [Fact]
    public void UnwrapDiscriminators_PreservesNonDiscriminatedDocuments_WithExtraFields()
    {
        var input = new BsonDocument
        {
            { "_t", "SomeType" },
            { "_v", new BsonArray() },
            { "extra", "field" }
        };

        var result = PersistenceDataMigration.UnwrapDiscriminators(input);

        result.AsBsonDocument.ElementCount.Should().Be(3);
        result.AsBsonDocument["extra"].AsString.Should().Be("field");
    }

    [Fact]
    public void UnwrapDiscriminators_PreservesDocumentWithOnlyT()
    {
        var input = new BsonDocument { { "_t", "System.Object[]" } };

        var result = PersistenceDataMigration.UnwrapDiscriminators(input);

        result.AsBsonDocument.ElementCount.Should().Be(1);
    }

    [Fact]
    public void UnwrapDiscriminators_PassesThroughPrimitiveValues()
    {
        PersistenceDataMigration.UnwrapDiscriminators(BsonString.Create("hello")).AsString.Should().Be("hello");
        PersistenceDataMigration.UnwrapDiscriminators(new BsonInt32(42)).AsInt32.Should().Be(42);
        PersistenceDataMigration.UnwrapDiscriminators(new BsonDouble(3.14)).AsDouble.Should().Be(3.14);
        PersistenceDataMigration.UnwrapDiscriminators(BsonBoolean.True).AsBoolean.Should().BeTrue();
        PersistenceDataMigration.UnwrapDiscriminators(BsonNull.Value).Should().Be(BsonNull.Value);
    }

    [Fact]
    public void UnwrapDiscriminators_HandlesEmptyDocument()
    {
        var input = new BsonDocument();

        var result = PersistenceDataMigration.UnwrapDiscriminators(input);

        result.AsBsonDocument.Should().BeEmpty();
    }

    [Fact]
    public void UnwrapDiscriminators_HandlesEmptyArray()
    {
        var input = new BsonArray();

        var result = PersistenceDataMigration.UnwrapDiscriminators(input);

        result.AsBsonArray.Should().BeEmpty();
    }

    [Fact]
    public void UnwrapDiscriminators_HandlesRealWorldInventory()
    {
        var input = new BsonArray
        {
            new BsonArray
            {
                new BsonDocument { { "_t", "System.Object[]" }, { "_v", new BsonArray { "UK3CB_BAF_M6" } } },
                new BsonDocument { { "_t", "System.Object[]" }, { "_v", new BsonArray { 1 } } }
            },
            new BsonArray
            {
                new BsonDocument { { "_t", "System.Object[]" }, { "_v", new BsonArray() } },
                new BsonDocument { { "_t", "System.Object[]" }, { "_v", new BsonArray() } }
            }
        };

        var result = PersistenceDataMigration.UnwrapDiscriminators(input);

        var weaponsContainer = result.AsBsonArray[0].AsBsonArray;
        weaponsContainer[0].AsBsonArray[0].AsString.Should().Be("UK3CB_BAF_M6");
        weaponsContainer[1].AsBsonArray[0].AsInt32.Should().Be(1);

        var magazinesContainer = result.AsBsonArray[1].AsBsonArray;
        magazinesContainer[0].AsBsonArray.Should().BeEmpty();
        magazinesContainer[1].AsBsonArray.Should().BeEmpty();
    }

    [Fact]
    public void UnwrapDiscriminators_HandlesRealWorldPlayerLoadout()
    {
        // Simulates the player loadout structure with nested _t/_v wrappers
        var input = new BsonDocument
        {
            {
                "loadout",
                new BsonArray
                {
                    new BsonDocument
                    {
                        { "_t", "System.Object[]" },
                        {
                            "_v",
                            new BsonArray
                            {
                                "U_B_CombatUniform_mcam", new BsonDocument { { "_t", "System.Object[]" }, { "_v", new BsonArray { "ItemMap" } } }
                            }
                        }
                    }
                }
            }
        };

        var result = PersistenceDataMigration.UnwrapDiscriminators(input);

        var loadout = result.AsBsonDocument["loadout"].AsBsonArray;
        var firstSlot = loadout[0].AsBsonArray;
        firstSlot[0].AsString.Should().Be("U_B_CombatUniform_mcam");
        firstSlot[1].AsBsonArray[0].AsString.Should().Be("ItemMap");
    }

    [Fact]
    public void UnwrapDiscriminators_HandlesTopLevelDocumentWithDiscriminatedFields()
    {
        var input = new BsonDocument
        {
            { "_id", ObjectId.GenerateNewId() },
            { "key", "test-session" },
            {
                "objects",
                new BsonArray
                {
                    new BsonDocument
                    {
                        { "id", "obj-1" },
                        {
                            "position", new BsonDocument
                            {
                                { "_t", "System.Object[]" },
                                {
                                    "_v", new BsonArray
                                    {
                                        100.5,
                                        200.3,
                                        0.1
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        var result = PersistenceDataMigration.UnwrapDiscriminators(input);

        result.AsBsonDocument["key"].AsString.Should().Be("test-session");
        var obj = result.AsBsonDocument["objects"].AsBsonArray[0].AsBsonDocument;
        var position = obj["position"].AsBsonArray;
        position[0].AsDouble.Should().Be(100.5);
        position[1].AsDouble.Should().Be(200.3);
        position[2].AsDouble.Should().Be(0.1);
    }

    [Fact]
    public void UnwrapDiscriminators_RecursivelyProcessesAllNestedLevels()
    {
        // Three levels of nesting
        var input = new BsonDocument
        {
            { "_t", "System.Object[]" },
            {
                "_v",
                new BsonArray
                {
                    new BsonDocument
                    {
                        { "_t", "System.Object[]" },
                        { "_v", new BsonArray { new BsonDocument { { "_t", "System.Object[]" }, { "_v", new BsonArray { "deep-value" } } } } }
                    }
                }
            }
        };

        var result = PersistenceDataMigration.UnwrapDiscriminators(input);

        var level1 = result.AsBsonArray;
        var level2 = level1[0].AsBsonArray;
        var level3 = level2[0].AsBsonArray;
        level3[0].AsString.Should().Be("deep-value");
    }

    [Fact]
    public void UnwrapDiscriminators_AlreadyCleanDocument_IsUnchanged()
    {
        var input = new BsonDocument
        {
            { "_id", ObjectId.GenerateNewId() },
            { "key", "clean-session" },
            { "objects", new BsonArray { new BsonDocument { { "id", "obj-1" }, { "type", "B_MRAP_01_F" } } } }
        };

        var result = PersistenceDataMigration.UnwrapDiscriminators(input);

        result.AsBsonDocument["key"].AsString.Should().Be("clean-session");
        result.AsBsonDocument["objects"].AsBsonArray[0].AsBsonDocument["id"].AsString.Should().Be("obj-1");
        result.AsBsonDocument["objects"].AsBsonArray[0].AsBsonDocument["type"].AsString.Should().Be("B_MRAP_01_F");
    }
}
