using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using UKSF.Api.Core.Models.Domain;
using Xunit;

namespace UKSF.Api.Core.Tests.Models.Domain;

public class DomainCommandRequestTests
{
    static DomainCommandRequestTests()
    {
        ConventionRegistry.Register(
            "DomainCommandRequestTestConventions",
            new ConventionPack
            {
                new IgnoreExtraElementsConvention(true),
                new IgnoreIfNullConvention(true),
                new CamelCaseElementNameConvention()
            },
            _ => true
        );
    }

    [Fact]
    public void NewlyConstructedRequest_HasNullOverrideFields()
    {
        var request = new DomainCommandRequest();
        request.OverriddenState.Should().BeNull();
        request.OverriddenBy.Should().BeNull();
    }

    [Fact]
    public void Roundtrip_PreservesOverrideFields()
    {
        var request = new DomainCommandRequest { OverriddenState = ReviewState.Approved, OverriddenBy = "5ed524b04f5b532a5437bba1" };
        var doc = request.ToBsonDocument();
        doc["overriddenBy"].BsonType.Should().Be(BsonType.ObjectId);
        var deserialised = BsonSerializer.Deserialize<DomainCommandRequest>(doc);
        deserialised.OverriddenState.Should().Be(ReviewState.Approved);
        deserialised.OverriddenBy.Should().Be("5ed524b04f5b532a5437bba1");
    }

    [Fact]
    public void OldArchiveDoc_WithoutOverrideFields_DeserialisesCleanly()
    {
        var legacyDoc = new BsonDocument
        {
            { "_id", ObjectId.GenerateNewId() },
            { "type", "Loa" },
            { "displayRecipient", "Cpl Bridg" },
            { "displayFrom", "2026-05-15" },
            { "displayValue", "2026-05-22" },
            { "reason", "holiday" },
            { "reviews", new BsonDocument { { "reviewer1", 0 } } }
        };
        var deserialised = BsonSerializer.Deserialize<DomainCommandRequest>(legacyDoc);
        deserialised.OverriddenState.Should().BeNull();
        deserialised.OverriddenBy.Should().BeNull();
        deserialised.Type.Should().Be("Loa");
    }
}
