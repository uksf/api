using System.Collections.Generic;
using FluentAssertions;
using UKSF.Api.ArmaServer.Converters;
using UKSF.Api.ArmaServer.Models.Persistence;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Converters;

public class PersistenceConverterTests
{
    [Fact]
    public void IsRawNamespaceFormat_WithRawKeys_ShouldReturnTrue()
    {
        var raw = new Dictionary<string, object> { { "uksf_persistence_objects", new List<object>() } };

        var result = PersistenceConverter.IsRawNamespaceFormat(raw);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsRawNamespaceFormat_WithStructuredKeys_ShouldReturnFalse()
    {
        var raw = new Dictionary<string, object>
        {
            { "objects", new List<object>() },
            { "players", new Dictionary<string, object>() },
            { "markers", new List<object>() }
        };

        var result = PersistenceConverter.IsRawNamespaceFormat(raw);

        result.Should().BeFalse();
    }

    [Fact]
    public void FromRawNamespace_WithFullSession_ShouldConvertAllCategories()
    {
        var raw = BuildMinimalNamespace();
        raw["uksf_persistence_objects"] = new List<object> { BuildMinimalObjectArray() };
        raw["uksf_persistence_deletedObjects"] = new List<object> { "deleted_1", "deleted_2" };
        raw["uksf_persistence_dateTime"] = new List<object>
        {
            2038L,
            6L,
            20L,
            2L,
            44L
        };
        raw["uksf_persistence_mapMarkers"] = new List<object>
        {
            new List<object>
            {
                "marker_1",
                new List<object> { 100.0, 200.0 },
                "RECTANGLE",
                "hd_dot",
                "Solid",
                new List<object> { 50.0, 50.0 },
                "ColorRed",
                1.0,
                0.0,
                "FOB Alpha"
            }
        };
        raw["76561198068932442"] = BuildMinimalPlayerArray();
        raw["uksf_safehouses_state"] = new List<object>
        {
            new List<object>(),
            new List<object>(),
            new List<object>()
        };

        var result = PersistenceConverter.FromRawNamespace(raw);

        result.Objects.Should().HaveCount(1);
        result.DeletedObjects.Should().HaveCount(2);
        result.DeletedObjects.Should().BeEquivalentTo(new[] { "deleted_1", "deleted_2" });
        result.ArmaDateTime.Should().BeEquivalentTo(new[] { 2038, 6, 20, 2, 44 });
        result.Markers.Should().HaveCount(1);
        result.Players.Should().HaveCount(1);
        result.Players.Should().ContainKey("76561198068932442");
        result.CustomData.Should().ContainKey("uksf_safehouses_state");
    }

    [Fact]
    public void FromRawNamespace_ShouldDetectPlayerUids()
    {
        var raw = BuildMinimalNamespace();
        raw["76561198068932442"] = BuildMinimalPlayerArray();
        raw["uksf_arearating_ratingAreas"] = new List<object>();

        var result = PersistenceConverter.FromRawNamespace(raw);

        result.Players.Should().HaveCount(1);
        result.Players.Should().ContainKey("76561198068932442");
        result.Players.Should().NotContainKey("uksf_arearating_ratingAreas");
    }

    [Fact]
    public void ToRawNamespace_RoundTrip_ShouldPreserveData()
    {
        var session = new DomainPersistenceSession
        {
            Objects =
            [
                new PersistenceObject
                {
                    Id = "obj_1",
                    Type = "B_supplyCrate_F",
                    Position = [100.0, 200.0, 0.0],
                    VectorDirUp = [new[] { 0.0, 1.0, 0.0 }, new[] { 0.0, 0.0, 1.0 }]
                }
            ],
            DeletedObjects = ["del_1", "del_2"],
            ArmaDateTime = [2038, 6, 20, 2, 44],
            Markers =
            [
                new List<object>
                {
                    "marker_1",
                    new List<object> { 100.0, 200.0 },
                    "RECTANGLE",
                    "hd_dot",
                    "Solid",
                    new List<object> { 50.0, 50.0 },
                    "ColorRed",
                    1.0,
                    0.0,
                    "FOB Alpha"
                }
            ],
            Players = new Dictionary<string, PlayerRedeployData>
            {
                {
                    "76561198068932442", new PlayerRedeployData
                    {
                        Position = [150.0, 250.0, 5.0],
                        Direction = 45.0,
                        Animation = "amovpercmstpsnonwnondnon"
                    }
                }
            },
            CustomData = new Dictionary<string, object> { { "uksf_safehouses_state", new List<object> { new List<object>() } } }
        };

        var raw = PersistenceConverter.ToRawNamespace(session);
        var roundTripped = PersistenceConverter.FromRawNamespace(raw);

        roundTripped.Objects.Should().HaveCount(1);
        roundTripped.Objects[0].Id.Should().Be("obj_1");
        roundTripped.Objects[0].Type.Should().Be("B_supplyCrate_F");
        roundTripped.DeletedObjects.Should().BeEquivalentTo(new[] { "del_1", "del_2" });
        roundTripped.ArmaDateTime.Should().BeEquivalentTo(new[] { 2038, 6, 20, 2, 44 });
        roundTripped.Markers.Should().HaveCount(1);
        roundTripped.Players.Should().ContainKey("76561198068932442");
        roundTripped.Players["76561198068932442"].Position.Should().BeEquivalentTo(new[] { 150.0, 250.0, 5.0 });
        roundTripped.Players["76561198068932442"].Direction.Should().Be(45.0);
        roundTripped.CustomData.Should().ContainKey("uksf_safehouses_state");
    }

    [Fact]
    public void RoundTrip_WithMarkers_ShouldPreserveBothFormats()
    {
        var standardMarker = new List<object>
        {
            "marker_1",
            new List<object> { 100.0, 200.0 },
            "RECTANGLE",
            "hd_dot",
            "Solid",
            new List<object> { 50.0, 50.0 },
            "ColorRed",
            1.0,
            0.0,
            "FOB Alpha"
        };

        var polylineMarker = new List<object>
        {
            "marker_2",
            new List<object>
            {
                0.0,
                0.0,
                0.0
            },
            "POLYLINE",
            "ColorBlue",
            0.8,
            new List<object>
            {
                new List<object>
                {
                    100.0,
                    200.0,
                    0.0
                },
                new List<object>
                {
                    300.0,
                    400.0,
                    0.0
                }
            }
        };

        var raw = BuildMinimalNamespace();
        raw["uksf_persistence_mapMarkers"] = new List<object> { standardMarker, polylineMarker };

        var session = PersistenceConverter.FromRawNamespace(raw);
        var rawRoundTripped = PersistenceConverter.ToRawNamespace(session);
        var finalSession = PersistenceConverter.FromRawNamespace(rawRoundTripped);

        finalSession.Markers.Should().HaveCount(2);
        finalSession.Markers[0].Should().HaveCount(10);
        finalSession.Markers[0][0].Should().Be("marker_1");
        finalSession.Markers[1].Should().HaveCount(6);
        finalSession.Markers[1][0].Should().Be("marker_2");
        finalSession.Markers[1][2].Should().Be("POLYLINE");
    }

    private static List<object> BuildMinimalObjectArray()
    {
        return
        [
            "", // 0: id
            "", // 1: type
            new List<object>
            {
                0.0,
                0.0,
                0.0
            }, // 2: position
            new List<object>
            {
                new List<object>
                {
                    0.0,
                    1.0,
                    0.0
                },
                new List<object>
                {
                    0.0,
                    0.0,
                    1.0
                }
            }, // 3: vectorDirUp
            0.0, // 4: damage
            1.0, // 5: fuel
            new List<object>(), // 6: turretWeapons
            new List<object>(), // 7: turretMagazines
            new List<object>(), // 8: pylonLoadout
            new List<object>
            {
                0.0,
                0.0,
                0.0
            }, // 9: logistics
            new List<object>(), // 10: attached
            new List<object>(), // 11: rackChannels
            new List<object>(), // 12: aceCargo
            new List<object> // 13: inventory
            {
                new List<object> { new List<object>(), new List<object>() },
                new List<object> { new List<object>(), new List<object>() },
                new List<object> { new List<object>(), new List<object>() },
                new List<object> { new List<object>(), new List<object>() }
            },
            new List<object> { false, "WEST" }, // 14: aceFortify
            new List<object>
            {
                0L,
                false,
                false
            }, // 15: aceMedical
            new List<object> { 0L, 0L }, // 16: aceRepair
            "" // 17: customName
        ];
    }

    private static List<object> BuildMinimalPlayerArray()
    {
        return
        [
            new List<object>
            {
                0.0,
                0.0,
                0.0
            }, // 0: position
            new List<object>
            {
                "",
                "",
                -1L
            }, // 1: vehicleState
            0.0, // 2: direction
            "", // 3: animation
            new List<object> // 4: loadout
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
            0.0, // 5: damage
            "{}", // 6: aceMedical
            false, // 7: earplugs
            new List<object>(), // 8: attachedItems
            new List<object>(), // 9: radios
            new List<object> { false } // 10: diveState
        ];
    }

    private static Dictionary<string, object> BuildMinimalNamespace()
    {
        return new Dictionary<string, object>
        {
            { "uksf_persistence_objects", new List<object>() },
            { "uksf_persistence_deletedObjects", new List<object>() },
            {
                "uksf_persistence_dateTime", new List<object>
                {
                    2038L,
                    6L,
                    20L,
                    2L,
                    44L
                }
            },
            { "uksf_persistence_mapMarkers", new List<object>() }
        };
    }
}
