using System.Collections.Generic;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using UKSF.Api.ArmaServer.Converters;
using UKSF.Api.ArmaServer.Models.Persistence;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Converters;

public class PersistencePlayerRadioTests
{
    [Fact]
    public void RadioState_WithMpu5Settings_ShouldRoundTrip()
    {
        var mpu5 = new List<object>
        {
            new List<object> { 6L },
            new List<object>
            {
                new List<object>
                {
                    6L,
                    0.5,
                    1L
                }
            },
            new List<object>
            {
                4L,
                0L,
                9L,
                0L
            }
        };
        var hashmap = BuildPlayerHashmap(
            new List<object>
            {
                new List<object>
                {
                    "ACRE_MPU5",
                    2L,
                    1.0,
                    "CENTER",
                    0L,
                    mpu5
                },
                new List<object>
                {
                    "ACRE_PRC152",
                    1L,
                    0.7,
                    "LEFT",
                    1L
                }
            }
        );

        var player = PersistencePlayerConverter.FromHashmap(hashmap);
        var radios = (List<object>)PersistencePlayerConverter.ToHashmap(player)["radios"];

        player.Radios[0].Mpu5.Should().BeEquivalentTo(mpu5);
        player.Radios[1].Mpu5.Should().BeEmpty();
        ((List<object>)radios[0]).Should().HaveCount(6);
        ((List<object>)radios[0])[5].Should().BeEquivalentTo(mpu5);
        ((List<object>)radios[1]).Should().HaveCount(5);
    }

    [Fact]
    public void RadioState_WithMpu5Settings_ShouldRoundTripThroughBson()
    {
        var radio = new RadioState
        {
            Type = "ACRE_MPU5",
            Mpu5 =
            [
                new List<object> { 6L }, new List<object>
                {
                    new List<object>
                    {
                        6L,
                        0.5,
                        1L
                    }
                },
                new List<object>
                {
                    4L,
                    0L,
                    9L,
                    0L
                }
            ]
        };

        var restored = BsonSerializer.Deserialize<RadioState>(radio.ToBsonDocument());

        restored.Mpu5.Should().BeEquivalentTo(radio.Mpu5);
    }

    private static Dictionary<string, object> BuildPlayerHashmap(List<object> radios) =>
        new()
        {
            ["position"] = new List<object>
            {
                0.0,
                0.0,
                0.0
            },
            ["vehicleState"] = new List<object>
            {
                "",
                "",
                -1L
            },
            ["direction"] = 0.0,
            ["animation"] = "",
            ["loadout"] = new List<object>
            {
                new List<object>(),
                new List<object>(),
                new List<object>(),
                new List<object>(),
                new List<object>(),
                new List<object>(),
                "",
                "",
                new List<object>(),
                new List<object>
                {
                    "",
                    "",
                    "",
                    "",
                    "",
                    ""
                }
            },
            ["damage"] = 0.0,
            ["aceMedical"] = new Dictionary<string, object>(),
            ["earplugs"] = false,
            ["attachedItems"] = new List<object>(),
            ["radios"] = radios,
            ["diveState"] = new List<object> { false }
        };
}
