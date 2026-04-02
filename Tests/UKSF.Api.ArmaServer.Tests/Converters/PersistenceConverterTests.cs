using System.Collections.Generic;
using FluentAssertions;
using UKSF.Api.ArmaServer.Converters;
using UKSF.Api.ArmaServer.Models.Persistence;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Converters;

public class PersistenceConverterTests
{
    [Fact]
    public void FromHashmap_WithFullSession_ShouldConvertAllCategories()
    {
        var raw = BuildMinimalSessionHashmap();
        raw["objects"] = new List<object> { BuildMinimalObjectHashmap() };
        raw["deletedObjects"] = new List<object> { "deleted_1", "deleted_2" };
        raw["dateTime"] = new List<object>
        {
            2038L,
            6L,
            20L,
            2L,
            44L
        };
        raw["mapMarkers"] = new List<object>
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
        raw["players"] = new Dictionary<string, object> { { "76561198068932442", BuildMinimalPlayerHashmap() } };
        raw["uksf_safehouses_state"] = new List<object>
        {
            new List<object>(),
            new List<object>(),
            new List<object>()
        };

        var result = PersistenceConverter.FromHashmap(raw);

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
    public void FromHashmap_WithPlayersNestedUnderPlayersKey_ShouldDetectPlayers()
    {
        var raw = BuildMinimalSessionHashmap();
        raw["players"] = new Dictionary<string, object> { { "76561198068932442", BuildMinimalPlayerHashmap() } };
        raw["uksf_arearating_ratingAreas"] = new List<object>();

        var result = PersistenceConverter.FromHashmap(raw);

        result.Players.Should().HaveCount(1);
        result.Players.Should().ContainKey("76561198068932442");
        result.CustomData.Should().ContainKey("uksf_arearating_ratingAreas");
        result.CustomData.Should().NotContainKey("players");
    }

    [Fact]
    public void FromHashmap_WithEmptyPlayers_ShouldReturnEmptyPlayersDictionary()
    {
        var raw = BuildMinimalSessionHashmap();

        var result = PersistenceConverter.FromHashmap(raw);

        result.Players.Should().BeEmpty();
    }

    [Fact]
    public void ToHashmap_RoundTrip_ShouldPreserveData()
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

        var raw = PersistenceConverter.ToHashmap(session);
        var roundTripped = PersistenceConverter.FromHashmap(raw);

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
    public void ToHashmap_ShouldUseNestedPlayersKey()
    {
        var session = new DomainPersistenceSession
        {
            Players = new Dictionary<string, PlayerRedeployData>
            {
                {
                    "76561198068932442", new PlayerRedeployData
                    {
                        Position = [0.0, 0.0, 0.0],
                        Direction = 0.0,
                        Animation = ""
                    }
                }
            }
        };

        var raw = PersistenceConverter.ToHashmap(session);

        raw.Should().ContainKey("players");
        raw.Should().NotContainKey("76561198068932442");
        var playersDict = raw["players"] as Dictionary<string, object>;
        playersDict.Should().NotBeNull();
        playersDict!.Should().ContainKey("76561198068932442");
    }

    [Fact]
    public void ToHashmap_ShouldUsePlainKeys()
    {
        var session = new DomainPersistenceSession();

        var raw = PersistenceConverter.ToHashmap(session);

        raw.Should().ContainKey("objects");
        raw.Should().ContainKey("deletedObjects");
        raw.Should().ContainKey("dateTime");
        raw.Should().ContainKey("mapMarkers");
        raw.Should().ContainKey("players");
        raw.Should().NotContainKey("uksf_persistence_objects");
        raw.Should().NotContainKey("uksf_persistence_deletedObjects");
        raw.Should().NotContainKey("uksf_persistence_dateTime");
        raw.Should().NotContainKey("uksf_persistence_mapMarkers");
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

        var raw = BuildMinimalSessionHashmap();
        raw["mapMarkers"] = new List<object> { standardMarker, polylineMarker };

        var session = PersistenceConverter.FromHashmap(raw);
        var rawRoundTripped = PersistenceConverter.ToHashmap(session);
        var finalSession = PersistenceConverter.FromHashmap(rawRoundTripped);

        finalSession.Markers.Should().HaveCount(2);
        finalSession.Markers[0].Should().HaveCount(10);
        finalSession.Markers[0][0].Should().Be("marker_1");
        finalSession.Markers[1].Should().HaveCount(6);
        finalSession.Markers[1][0].Should().Be("marker_2");
        finalSession.Markers[1][2].Should().Be("POLYLINE");
    }

    [Fact]
    public void FromHashmap_WithRealObjectData_ShouldRoundTrip()
    {
        var realObject = new Dictionary<string, object>
        {
            ["id"] = "uksf_resupply_r4_728144_65204",
            ["type"] = "uksf_resupply_r4",
            ["position"] = new List<object>
            {
                11614.5,
                3722.82,
                20.8851
            },
            ["vectorDirUp"] = new List<object>
            {
                new List<object>
                {
                    -0.656394,
                    -0.754382,
                    0.00731854
                },
                new List<object>
                {
                    0.00356233,
                    0.00660148,
                    0.999972
                }
            },
            ["damage"] = 0.0158537,
            ["fuel"] = 1.0,
            ["turretWeapons"] = new List<object>(),
            ["turretMagazines"] = new List<object>(),
            ["pylonLoadout"] = new List<object>(),
            ["logistics"] = new List<object>
            {
                -1.0,
                -1.0,
                -1.0
            },
            ["attached"] = new List<object>(),
            ["rackChannels"] = new List<object>(),
            ["aceCargo"] = new List<object>
            {
                new List<object>
                {
                    "uksf_resupply_g14",
                    new List<object>(),
                    new List<object>(),
                    ""
                },
                new List<object>
                {
                    "uksf_resupply_g14",
                    new List<object>(),
                    new List<object>(),
                    ""
                },
                new List<object>
                {
                    "uksf_resupply_g14",
                    new List<object>(),
                    new List<object>(),
                    ""
                }
            },
            ["inventory"] = new List<object>
            {
                new List<object> { new List<object>(), new List<object>() },
                new List<object> { new List<object>(), new List<object>() },
                new List<object> { new List<object>(), new List<object>() },
                new List<object> { new List<object>(), new List<object>() }
            },
            ["aceFortify"] = new List<object> { false, "WEST" },
            ["aceMedical"] = new List<object>
            {
                0L,
                false,
                false
            },
            ["aceRepair"] = new List<object> { 0L, 0L },
            ["customName"] = ""
        };

        var raw = BuildMinimalSessionHashmap();
        raw["objects"] = new List<object> { realObject };
        raw["dateTime"] = new List<object>
        {
            2038L,
            6L,
            20L,
            2L,
            44L
        };

        var session = PersistenceConverter.FromHashmap(raw);

        session.Objects.Should().HaveCount(1);
        var obj = session.Objects[0];
        obj.Id.Should().Be("uksf_resupply_r4_728144_65204");
        obj.Type.Should().Be("uksf_resupply_r4");
        obj.Position.Should().BeEquivalentTo(new[] { 11614.5, 3722.82, 20.8851 });
        obj.Damage.Should().BeApproximately(0.0158537, 0.0001);
        obj.Fuel.Should().Be(1.0);
        obj.AceCargo.Should().HaveCount(3);
        obj.AceCargo[0].ClassName.Should().Be("uksf_resupply_g14");
        obj.AceCargo[1].ClassName.Should().Be("uksf_resupply_g14");
        obj.AceCargo[2].ClassName.Should().Be("uksf_resupply_g14");
        obj.Logistics.Should().BeEquivalentTo(new[] { -1.0, -1.0, -1.0 });
        obj.AceFortify.IsAceFortification.Should().BeFalse();
        obj.AceFortify.Side.Should().Be("WEST");

        var rawRoundTripped = PersistenceConverter.ToHashmap(session);
        var sessionRoundTripped = PersistenceConverter.FromHashmap(rawRoundTripped);

        var objRt = sessionRoundTripped.Objects[0];
        objRt.Id.Should().Be(obj.Id);
        objRt.Type.Should().Be(obj.Type);
        objRt.Position.Should().BeEquivalentTo(obj.Position);
        objRt.Damage.Should().BeApproximately(obj.Damage, 0.0001);
        objRt.Fuel.Should().Be(obj.Fuel);
        objRt.AceCargo.Should().HaveCount(3);
        objRt.AceCargo[0].ClassName.Should().Be("uksf_resupply_g14");
        objRt.Logistics.Should().BeEquivalentTo(obj.Logistics);
    }

    [Fact]
    public void FromHashmap_WithRealVehicleData_ShouldHandleTurrets()
    {
        var vehicle = new Dictionary<string, object>
        {
            ["id"] = "UK3CB_BAF_LandRover_Hard_FFR_Sand_A_662960_75545",
            ["type"] = "UK3CB_BAF_LandRover_Hard_FFR_Sand_A",
            ["position"] = new List<object>
            {
                11486.1,
                2392.07,
                23.017
            },
            ["vectorDirUp"] = new List<object>
            {
                new List<object>
                {
                    -0.812788,
                    -0.582542,
                    -0.00454635
                },
                new List<object>
                {
                    -0.00650863,
                    0.00127696,
                    0.999978
                }
            },
            ["damage"] = 0.0,
            ["fuel"] = 0.998611,
            ["turretWeapons"] = new List<object> { new List<object> { new List<object> { 0L }, new List<object> { "UK3CB_BAF_L7A2" } } },
            ["turretMagazines"] = new List<object>
            {
                new List<object>
                {
                    "UK3CB_BAF_200Rnd_762_T",
                    new List<object> { 0L },
                    200L,
                    1234L,
                    5678L
                }
            },
            ["pylonLoadout"] = new List<object>(),
            ["logistics"] = new List<object>
            {
                -1.0,
                -1.0,
                -1.0
            },
            ["attached"] = new List<object>(),
            ["rackChannels"] = new List<object>(),
            ["aceCargo"] = new List<object>
            {
                new List<object>
                {
                    "ACE_Wheel",
                    new List<object>(),
                    new List<object>(),
                    ""
                },
                new List<object>
                {
                    "ACE_Wheel",
                    new List<object>(),
                    new List<object>(),
                    ""
                }
            },
            ["inventory"] = new List<object>
            {
                new List<object> { new List<object>(), new List<object>() },
                new List<object> { new List<object>(), new List<object>() },
                new List<object> { new List<object> { "FirstAidKit" }, new List<object> { 4L } },
                new List<object> { new List<object>(), new List<object>() }
            },
            ["aceFortify"] = new List<object> { false, "WEST" },
            ["aceMedical"] = new List<object>
            {
                0L,
                false,
                false
            },
            ["aceRepair"] = new List<object> { 0L, 0L },
            ["customName"] = ""
        };

        var raw = BuildMinimalSessionHashmap();
        raw["objects"] = new List<object> { vehicle };

        var session = PersistenceConverter.FromHashmap(raw);

        session.Objects.Should().HaveCount(1);
        var obj = session.Objects[0];

        obj.Id.Should().Be("UK3CB_BAF_LandRover_Hard_FFR_Sand_A_662960_75545");
        obj.Type.Should().Be("UK3CB_BAF_LandRover_Hard_FFR_Sand_A");
        obj.Fuel.Should().BeApproximately(0.998611, 0.0001);

        obj.TurretWeapons.Should().HaveCount(1);
        obj.TurretWeapons[0].TurretPath.Should().BeEquivalentTo(new[] { 0 });
        obj.TurretWeapons[0].Weapons.Should().BeEquivalentTo(new[] { "UK3CB_BAF_L7A2" });

        obj.TurretMagazines.Should().HaveCount(1);
        obj.TurretMagazines[0].ClassName.Should().Be("UK3CB_BAF_200Rnd_762_T");
        obj.TurretMagazines[0].TurretPath.Should().BeEquivalentTo(new[] { 0 });
        obj.TurretMagazines[0].AmmoCount.Should().Be(200);

        obj.Inventory.Items.ClassNames.Should().BeEquivalentTo(new[] { "FirstAidKit" });
        obj.Inventory.Items.Counts.Should().BeEquivalentTo(new[] { 4 });

        obj.AceCargo.Should().HaveCount(2);
        obj.AceCargo[0].ClassName.Should().Be("ACE_Wheel");
        obj.AceCargo[1].ClassName.Should().Be("ACE_Wheel");
    }

    [Fact]
    public void FullRoundTrip_HashmapToSessionAndBack_ShouldBeConsistent()
    {
        var supplyCrate = new Dictionary<string, object>
        {
            ["id"] = "uksf_resupply_r4_728144_65204",
            ["type"] = "uksf_resupply_r4",
            ["position"] = new List<object>
            {
                11614.5,
                3722.82,
                20.8851
            },
            ["vectorDirUp"] = new List<object>
            {
                new List<object>
                {
                    -0.656394,
                    -0.754382,
                    0.00731854
                },
                new List<object>
                {
                    0.00356233,
                    0.00660148,
                    0.999972
                }
            },
            ["damage"] = 0.0158537,
            ["fuel"] = 1.0,
            ["turretWeapons"] = new List<object>(),
            ["turretMagazines"] = new List<object>(),
            ["pylonLoadout"] = new List<object>(),
            ["logistics"] = new List<object>
            {
                -1.0,
                -1.0,
                -1.0
            },
            ["attached"] = new List<object>(),
            ["rackChannels"] = new List<object>(),
            ["aceCargo"] = new List<object>
            {
                new List<object>
                {
                    "uksf_resupply_g14",
                    new List<object>(),
                    new List<object>(),
                    ""
                },
                new List<object>
                {
                    "uksf_resupply_g14",
                    new List<object>(),
                    new List<object>(),
                    ""
                },
                new List<object>
                {
                    "uksf_resupply_g14",
                    new List<object>(),
                    new List<object>(),
                    ""
                }
            },
            ["inventory"] = new List<object>
            {
                new List<object> { new List<object>(), new List<object>() },
                new List<object> { new List<object>(), new List<object>() },
                new List<object> { new List<object>(), new List<object>() },
                new List<object> { new List<object>(), new List<object>() }
            },
            ["aceFortify"] = new List<object> { false, "WEST" },
            ["aceMedical"] = new List<object>
            {
                0L,
                false,
                false
            },
            ["aceRepair"] = new List<object> { 0L, 0L },
            ["customName"] = ""
        };

        var vehicle = new Dictionary<string, object>
        {
            ["id"] = "UK3CB_BAF_LandRover_Hard_FFR_Sand_A_662960_75545",
            ["type"] = "UK3CB_BAF_LandRover_Hard_FFR_Sand_A",
            ["position"] = new List<object>
            {
                11486.1,
                2392.07,
                23.017
            },
            ["vectorDirUp"] = new List<object>
            {
                new List<object>
                {
                    -0.812788,
                    -0.582542,
                    -0.00454635
                },
                new List<object>
                {
                    -0.00650863,
                    0.00127696,
                    0.999978
                }
            },
            ["damage"] = 0.0,
            ["fuel"] = 0.998611,
            ["turretWeapons"] = new List<object> { new List<object> { new List<object> { 0L }, new List<object> { "UK3CB_BAF_L7A2" } } },
            ["turretMagazines"] = new List<object>
            {
                new List<object>
                {
                    "UK3CB_BAF_200Rnd_762_T",
                    new List<object> { 0L },
                    200L,
                    1234L,
                    5678L
                }
            },
            ["pylonLoadout"] = new List<object>(),
            ["logistics"] = new List<object>
            {
                -1.0,
                -1.0,
                -1.0
            },
            ["attached"] = new List<object>(),
            ["rackChannels"] = new List<object>(),
            ["aceCargo"] = new List<object>
            {
                new List<object>
                {
                    "ACE_Wheel",
                    new List<object>(),
                    new List<object>(),
                    ""
                },
                new List<object>
                {
                    "ACE_Wheel",
                    new List<object>(),
                    new List<object>(),
                    ""
                }
            },
            ["inventory"] = new List<object>
            {
                new List<object> { new List<object>(), new List<object>() },
                new List<object> { new List<object>(), new List<object>() },
                new List<object> { new List<object> { "FirstAidKit" }, new List<object> { 4L } },
                new List<object> { new List<object>(), new List<object>() }
            },
            ["aceFortify"] = new List<object> { false, "WEST" },
            ["aceMedical"] = new List<object>
            {
                0L,
                false,
                false
            },
            ["aceRepair"] = new List<object> { 0L, 0L },
            ["customName"] = ""
        };

        var playerHashmap = new Dictionary<string, object>
        {
            ["position"] = new List<object>
            {
                11500.0,
                2400.0,
                25.0
            },
            ["vehicleState"] = new List<object>
            {
                "",
                "",
                -1L
            },
            ["direction"] = 135.5,
            ["animation"] = "amovpercmstpsnonwnondnon",
            ["loadout"] = new List<object>
            {
                new List<object>
                {
                    "arifle_MX_F",
                    "",
                    "",
                    "optic_Aco",
                    new List<object> { "30Rnd_65x39_caseless_mag", 30L },
                    new List<object>(),
                    ""
                },
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
                    "ItemMap",
                    "ItemGPS",
                    "ItemRadio",
                    "ItemCompass",
                    "ItemWatch",
                    ""
                }
            },
            ["damage"] = 0.15,
            ["aceMedical"] = "{}",
            ["earplugs"] = false,
            ["attachedItems"] = new List<object>(),
            ["radios"] = new List<object>(),
            ["diveState"] = new List<object> { false }
        };

        var marker = new List<object>
        {
            "fob_alpha",
            new List<object> { 11500.0, 2400.0 },
            "RECTANGLE",
            "hd_dot",
            "Solid",
            new List<object> { 50.0, 50.0 },
            "ColorRed",
            1.0,
            0.0,
            "FOB Alpha"
        };

        var raw = new Dictionary<string, object>
        {
            { "objects", new List<object> { supplyCrate, vehicle } },
            { "deletedObjects", new List<object> { "deleted_obj_1" } },
            {
                "dateTime", new List<object>
                {
                    2038L,
                    6L,
                    20L,
                    2L,
                    44L
                }
            },
            { "mapMarkers", new List<object> { marker } },
            { "players", new Dictionary<string, object> { { "76561198068932442", playerHashmap } } },
            {
                "uksf_safehouses_state", new List<object>
                {
                    new List<object>(),
                    new List<object>(),
                    new List<object>()
                }
            }
        };

        var session1 = PersistenceConverter.FromHashmap(raw);
        var rawRoundTripped = PersistenceConverter.ToHashmap(session1);
        var session2 = PersistenceConverter.FromHashmap(rawRoundTripped);

        session2.Objects.Should().HaveCount(session1.Objects.Count);
        session2.Players.Should().HaveCount(session1.Players.Count);
        session2.ArmaDateTime.Should().BeEquivalentTo(session1.ArmaDateTime);
        session2.Markers.Should().HaveCount(session1.Markers.Count);
        session2.DeletedObjects.Should().BeEquivalentTo(session1.DeletedObjects);
        session2.CustomData.Should().HaveCount(session1.CustomData.Count);

        session2.Objects[0].Id.Should().Be("uksf_resupply_r4_728144_65204");
        session2.Objects[0].AceCargo.Should().HaveCount(3);
        session2.Objects[1].Id.Should().Be("UK3CB_BAF_LandRover_Hard_FFR_Sand_A_662960_75545");
        session2.Objects[1].TurretWeapons.Should().HaveCount(1);
        session2.Objects[1].TurretMagazines.Should().HaveCount(1);

        session2.Players.Should().ContainKey("76561198068932442");
        var player = session2.Players["76561198068932442"];
        player.Position.Should().BeEquivalentTo(new[] { 11500.0, 2400.0, 25.0 });
        player.Direction.Should().Be(135.5);
        player.Animation.Should().Be("amovpercmstpsnonwnondnon");
        player.Loadout.PrimaryWeapon.Weapon.Should().Be("arifle_MX_F");
        player.Damage.Should().BeApproximately(0.15, 0.001);
    }

    private static Dictionary<string, object> BuildMinimalObjectHashmap() =>
        new()
        {
            ["id"] = "",
            ["type"] = "",
            ["position"] = new List<object>
            {
                0.0,
                0.0,
                0.0
            },
            ["vectorDirUp"] = new List<object>
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
            },
            ["damage"] = 0.0,
            ["fuel"] = 1.0,
            ["turretWeapons"] = new List<object>(),
            ["turretMagazines"] = new List<object>(),
            ["pylonLoadout"] = new List<object>(),
            ["logistics"] = new List<object>
            {
                0.0,
                0.0,
                0.0
            },
            ["attached"] = new List<object>(),
            ["rackChannels"] = new List<object>(),
            ["aceCargo"] = new List<object>(),
            ["inventory"] = new List<object>
            {
                new List<object> { new List<object>(), new List<object>() },
                new List<object> { new List<object>(), new List<object>() },
                new List<object> { new List<object>(), new List<object>() },
                new List<object> { new List<object>(), new List<object>() }
            },
            ["aceFortify"] = new List<object> { false, "WEST" },
            ["aceMedical"] = new List<object>
            {
                0L,
                false,
                false
            },
            ["aceRepair"] = new List<object> { 0L, 0L },
            ["customName"] = ""
        };

    private static Dictionary<string, object> BuildMinimalPlayerHashmap() =>
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
            ["aceMedical"] = "{}",
            ["earplugs"] = false,
            ["attachedItems"] = new List<object>(),
            ["radios"] = new List<object>(),
            ["diveState"] = new List<object> { false }
        };

    private static Dictionary<string, object> BuildMinimalSessionHashmap()
    {
        return new Dictionary<string, object>
        {
            { "objects", new List<object>() },
            { "deletedObjects", new List<object>() },
            {
                "dateTime", new List<object>
                {
                    2038L,
                    6L,
                    20L,
                    2L,
                    44L
                }
            },
            { "mapMarkers", new List<object>() },
            { "players", new Dictionary<string, object>() }
        };
    }
}
