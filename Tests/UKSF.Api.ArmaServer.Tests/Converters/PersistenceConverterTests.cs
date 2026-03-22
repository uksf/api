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

    [Fact]
    public void FromRawNamespace_WithRealObjectData_ShouldRoundTrip()
    {
        var realObject = new List<object>
        {
            "uksf_resupply_r4_728144_65204",
            "uksf_resupply_r4",
            new List<object>
            {
                11614.5,
                3722.82,
                20.8851
            },
            new List<object>
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
            0.0158537,
            1.0,
            new List<object>(),
            new List<object>(),
            new List<object>(),
            new List<object>
            {
                -1.0,
                -1.0,
                -1.0
            },
            new List<object>(),
            new List<object>(),
            new List<object>
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
            new List<object>
            {
                new List<object> { new List<object>(), new List<object>() },
                new List<object> { new List<object>(), new List<object>() },
                new List<object> { new List<object>(), new List<object>() },
                new List<object> { new List<object>(), new List<object>() }
            },
            new List<object> { false, "WEST" },
            new List<object>
            {
                0L,
                false,
                false
            },
            new List<object> { 0L, 0L },
            ""
        };

        var raw = BuildMinimalNamespace();
        raw["uksf_persistence_objects"] = new List<object> { realObject };
        raw["uksf_persistence_dateTime"] = new List<object>
        {
            2038L,
            6L,
            20L,
            2L,
            44L
        };

        var session = PersistenceConverter.FromRawNamespace(raw);

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

        var rawRoundTripped = PersistenceConverter.ToRawNamespace(session);
        var sessionRoundTripped = PersistenceConverter.FromRawNamespace(rawRoundTripped);

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
    public void FromRawNamespace_WithRealVehicleData_ShouldHandleTurrets()
    {
        var vehicle = new List<object>
        {
            "UK3CB_BAF_LandRover_Hard_FFR_Sand_A_662960_75545",
            "UK3CB_BAF_LandRover_Hard_FFR_Sand_A",
            new List<object>
            {
                11486.1,
                2392.07,
                23.017
            },
            new List<object>
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
            0.0,
            0.998611,
            new List<object> { new List<object> { new List<object> { 0L }, new List<object> { "UK3CB_BAF_L7A2" } } },
            new List<object>
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
            new List<object>(),
            new List<object>
            {
                -1.0,
                -1.0,
                -1.0
            },
            new List<object>(),
            new List<object>(),
            new List<object>
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
            new List<object>
            {
                new List<object> { new List<object>(), new List<object>() },
                new List<object> { new List<object>(), new List<object>() },
                new List<object> { new List<object> { "FirstAidKit" }, new List<object> { 4L } },
                new List<object> { new List<object>(), new List<object>() }
            },
            new List<object> { false, "WEST" },
            new List<object>
            {
                0L,
                false,
                false
            },
            new List<object> { 0L, 0L },
            ""
        };

        var raw = BuildMinimalNamespace();
        raw["uksf_persistence_objects"] = new List<object> { vehicle };

        var session = PersistenceConverter.FromRawNamespace(raw);

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
    public void FullRoundTrip_RawNamespaceToSessionAndBack_ShouldBeConsistent()
    {
        var supplyCrate = new List<object>
        {
            "uksf_resupply_r4_728144_65204",
            "uksf_resupply_r4",
            new List<object>
            {
                11614.5,
                3722.82,
                20.8851
            },
            new List<object>
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
            0.0158537,
            1.0,
            new List<object>(),
            new List<object>(),
            new List<object>(),
            new List<object>
            {
                -1.0,
                -1.0,
                -1.0
            },
            new List<object>(),
            new List<object>(),
            new List<object>
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
            new List<object>
            {
                new List<object> { new List<object>(), new List<object>() },
                new List<object> { new List<object>(), new List<object>() },
                new List<object> { new List<object>(), new List<object>() },
                new List<object> { new List<object>(), new List<object>() }
            },
            new List<object> { false, "WEST" },
            new List<object>
            {
                0L,
                false,
                false
            },
            new List<object> { 0L, 0L },
            ""
        };

        var vehicle = new List<object>
        {
            "UK3CB_BAF_LandRover_Hard_FFR_Sand_A_662960_75545",
            "UK3CB_BAF_LandRover_Hard_FFR_Sand_A",
            new List<object>
            {
                11486.1,
                2392.07,
                23.017
            },
            new List<object>
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
            0.0,
            0.998611,
            new List<object> { new List<object> { new List<object> { 0L }, new List<object> { "UK3CB_BAF_L7A2" } } },
            new List<object>
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
            new List<object>(),
            new List<object>
            {
                -1.0,
                -1.0,
                -1.0
            },
            new List<object>(),
            new List<object>(),
            new List<object>
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
            new List<object>
            {
                new List<object> { new List<object>(), new List<object>() },
                new List<object> { new List<object>(), new List<object>() },
                new List<object> { new List<object> { "FirstAidKit" }, new List<object> { 4L } },
                new List<object> { new List<object>(), new List<object>() }
            },
            new List<object> { false, "WEST" },
            new List<object>
            {
                0L,
                false,
                false
            },
            new List<object> { 0L, 0L },
            ""
        };

        var playerArray = new List<object>
        {
            new List<object>
            {
                11500.0,
                2400.0,
                25.0
            },
            new List<object>
            {
                "",
                "",
                -1L
            },
            135.5,
            "amovpercmstpsnonwnondnon",
            new List<object>
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
            0.15,
            "{}",
            false,
            new List<object>(),
            new List<object>(),
            new List<object> { false }
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
            { "uksf_persistence_objects", new List<object> { supplyCrate, vehicle } },
            { "uksf_persistence_deletedObjects", new List<object> { "deleted_obj_1" } },
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
            { "uksf_persistence_mapMarkers", new List<object> { marker } },
            { "76561198068932442", playerArray },
            {
                "uksf_safehouses_state", new List<object>
                {
                    new List<object>(),
                    new List<object>(),
                    new List<object>()
                }
            }
        };

        var session1 = PersistenceConverter.FromRawNamespace(raw);
        var rawRoundTripped = PersistenceConverter.ToRawNamespace(session1);
        var session2 = PersistenceConverter.FromRawNamespace(rawRoundTripped);

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
