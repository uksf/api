using System.Collections.Generic;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using UKSF.Api.ArmaServer.Models.Persistence;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Models.Persistence;

public class PlainBsonSerializerTests
{
    private static BsonDocument SerializeToDocument<T>(T value, IBsonSerializer<T> serializer)
    {
        var doc = new BsonDocument();
        using var writer = new BsonDocumentWriter(doc);
        writer.WriteStartDocument();
        writer.WriteName("value");
        var context = BsonSerializationContext.CreateRoot(writer);
        serializer.Serialize(context, value);
        writer.WriteEndDocument();
        return doc;
    }

    private static T DeserializeFromDocument<T>(BsonDocument doc, IBsonSerializer<T> serializer)
    {
        using var reader = new BsonDocumentReader(doc);
        reader.ReadStartDocument();
        reader.ReadName();
        var context = BsonDeserializationContext.CreateRoot(reader);
        var result = serializer.Deserialize(context);
        reader.ReadEndDocument();
        return result;
    }

    private static T RoundTrip<T>(T value, IBsonSerializer<T> serializer)
    {
        var doc = SerializeToDocument(value, serializer);
        return DeserializeFromDocument(doc, serializer);
    }

    // PlainObjectDictionaryBsonSerializer tests

    [Fact]
    public void PlainObjectDictionary_WithListValue_RoundTrips()
    {
        Dictionary<string, object> input = new()
        {
            {
                "key", new List<object>
                {
                    1,
                    2,
                    3
                }
            }
        };

        var result = RoundTrip(input, PlainObjectDictionaryBsonSerializer.Instance);

        result.Should().ContainKey("key");
        var list = result["key"].Should().BeOfType<List<object>>().Subject;
        list.Should()
        .BeEquivalentTo(
            new List<object>
            {
                1,
                2,
                3
            }
        );
    }

    [Fact]
    public void PlainObjectDictionary_MixedValues_RoundTrips()
    {
        Dictionary<string, object> input = new()
        {
            { "str", "hello" },
            { "num", 42 },
            { "flag", true }
        };

        var result = RoundTrip(input, PlainObjectDictionaryBsonSerializer.Instance);

        result["str"].Should().Be("hello");
        result["num"].Should().Be(42);
        result["flag"].Should().Be(true);
    }

    [Fact]
    public void PlainObjectDictionary_Empty_RoundTrips()
    {
        Dictionary<string, object> input = [];

        var result = RoundTrip(input, PlainObjectDictionaryBsonSerializer.Instance);

        result.Should().BeEmpty();
    }

    [Fact]
    public void PlainObjectDictionary_WritesPlainDocument_NoDiscriminators()
    {
        Dictionary<string, object> input = new() { { "x", 99 } };

        var doc = SerializeToDocument(input, PlainObjectDictionaryBsonSerializer.Instance);
        var bsonValue = doc["value"];

        bsonValue.BsonType.Should().Be(BsonType.Document);
        bsonValue.AsBsonDocument["x"].AsInt32.Should().Be(99);
    }

    // PlainObjectNestedListBsonSerializer tests

    [Fact]
    public void PlainObjectNestedList_RoundTrips()
    {
        List<List<object>> input =
        [
            ["marker1", 100.0, 200.0],
            ["marker2", 300.0, 400.0]
        ];

        var result = RoundTrip(input, PlainObjectNestedListBsonSerializer.Instance);

        result.Should().HaveCount(2);
        result[0]
        .Should()
        .BeEquivalentTo(
            new List<object>
            {
                "marker1",
                100.0,
                200.0
            }
        );
        result[1]
        .Should()
        .BeEquivalentTo(
            new List<object>
            {
                "marker2",
                300.0,
                400.0
            }
        );
    }

    [Fact]
    public void PlainObjectNestedList_Empty_RoundTrips()
    {
        List<List<object>> input = [];

        var result = RoundTrip(input, PlainObjectNestedListBsonSerializer.Instance);

        result.Should().BeEmpty();
    }

    [Fact]
    public void PlainObjectNestedList_WritesNestedArrays_NoDiscriminators()
    {
        List<List<object>> input = [["x", 1]];

        var doc = SerializeToDocument(input, PlainObjectNestedListBsonSerializer.Instance);
        var outerArray = doc["value"].AsBsonArray;

        outerArray.Should().HaveCount(1);
        outerArray[0].BsonType.Should().Be(BsonType.Array);
        outerArray[0].AsBsonArray[0].AsString.Should().Be("x");
        outerArray[0].AsBsonArray[1].AsInt32.Should().Be(1);
    }
}
