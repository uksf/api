using System.Collections.Generic;
using FluentAssertions;
using UKSF.Api.ArmaServer.Converters;
using UKSF.Api.ArmaServer.Models.Persistence;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Converters;

public class PersistencePlayerConverterTests
{
    [Fact]
    public void FromHashmap_WithBasicPlayer_ShouldConvertAllFields()
    {
        var hashmap = BuildMinimalPlayerHashmap();
        hashmap["position"] = new List<object>
        {
            100.0,
            200.0,
            30.0
        };
        hashmap["vehicleState"] = new List<object>
        {
            "",
            "",
            -1L
        };
        hashmap["direction"] = 45.0;
        hashmap["animation"] = "amovpercmstpsnonwnondnon";
        hashmap["loadout"] = new List<object>
        {
            new List<object>
            {
                "arifle_MX_F",
                "",
                "",
                "",
                new List<object> { "30Rnd_65x39_caseless_mag", 30L },
                new List<object>(),
                ""
            },
            new List<object>(),
            new List<object>(),
            new List<object> { "U_B_CombatUniform_mcam", new List<object> { new List<object> { "FirstAidKit", 1L } } },
            new List<object>(),
            new List<object>(),
            "H_HelmetB",
            "",
            new List<object>(),
            new List<object>
            {
                "ItemMap",
                "ItemGPS",
                "ItemRadio",
                "ItemCompass",
                "ItemWatch",
                "NVGoggles"
            }
        };
        hashmap["damage"] = 0.25;
        hashmap["aceMedical"] = new Dictionary<string, object> { ["ace_medical_bloodVolume"] = 6.0, ["ace_medical_heartRate"] = 80L };
        hashmap["earplugs"] = true;
        hashmap["attachedItems"] = new List<object> { "ACE_IR_Strobe_Item" };
        hashmap["radios"] = new List<object>
        {
            new List<object>
            {
                "ACRE_PRC152",
                1L,
                0.8,
                "CENTER",
                0L
            }
        };
        hashmap["diveState"] = new List<object> { false };

        var result = PersistencePlayerConverter.FromHashmap(hashmap);

        result.Position.Should().BeEquivalentTo(new[] { 100.0, 200.0, 30.0 });
        result.VehicleState.VehicleId.Should().BeEmpty();
        result.VehicleState.Role.Should().BeEmpty();
        result.VehicleState.Index.Should().Be(-1);
        result.Direction.Should().Be(45.0);
        result.Animation.Should().Be("amovpercmstpsnonwnondnon");
        result.Loadout.PrimaryWeapon.Weapon.Should().Be("arifle_MX_F");
        result.Loadout.PrimaryWeapon.PrimaryMagazine.ClassName.Should().Be("30Rnd_65x39_caseless_mag");
        result.Loadout.PrimaryWeapon.PrimaryMagazine.Ammo.Should().Be(30);
        result.Loadout.Uniform.ClassName.Should().Be("U_B_CombatUniform_mcam");
        result.Loadout.Uniform.Items.Should().HaveCount(1);
        result.Loadout.Uniform.Items[0].ClassName.Should().Be("FirstAidKit");
        result.Loadout.Uniform.Items[0].Count.Should().Be(1);
        result.Loadout.Headgear.Should().Be("H_HelmetB");
        result.Loadout.LinkedItems.Map.Should().Be("ItemMap");
        result.Loadout.LinkedItems.Gps.Should().Be("ItemGPS");
        result.Loadout.LinkedItems.Radio.Should().Be("ItemRadio");
        result.Loadout.LinkedItems.Compass.Should().Be("ItemCompass");
        result.Loadout.LinkedItems.Watch.Should().Be("ItemWatch");
        result.Loadout.LinkedItems.Nvg.Should().Be("NVGoggles");
        result.Damage.Should().Be(0.25);
        result.AceMedical.BloodVolume.Should().Be(6.0);
        result.AceMedical.HeartRate.Should().Be(80);
        result.Earplugs.Should().BeTrue();
        result.AttachedItems.Should().BeEquivalentTo(new[] { "ACE_IR_Strobe_Item" });
        result.Radios.Should().HaveCount(1);
        result.Radios[0].Type.Should().Be("ACRE_PRC152");
        result.Radios[0].Channel.Should().Be(1);
        result.Radios[0].Volume.Should().Be(0.8);
        result.Radios[0].Spatial.Should().Be("CENTER");
        result.Radios[0].PttIndex.Should().Be(0);
        result.DiveState.IsDiving.Should().BeFalse();
        result.DiveState.RawData.Should().BeEmpty();
    }

    [Fact]
    public void FromHashmap_WithTurretVehicleState_ShouldHandleIntArrayIndex()
    {
        var hashmap = BuildMinimalPlayerHashmap();
        hashmap["vehicleState"] = new List<object>
        {
            "veh_123",
            "turret",
            new List<object> { 0L, 1L }
        };

        var result = PersistencePlayerConverter.FromHashmap(hashmap);

        result.VehicleState.VehicleId.Should().Be("veh_123");
        result.VehicleState.Role.Should().Be("turret");
        result.VehicleState.Index.Should().BeEquivalentTo(new[] { 0, 1 });
    }

    [Fact]
    public void FromHashmap_WithCargoVehicleState_ShouldHandleIntIndex()
    {
        var hashmap = BuildMinimalPlayerHashmap();
        hashmap["vehicleState"] = new List<object>
        {
            "veh_456",
            "cargo",
            3L
        };

        var result = PersistencePlayerConverter.FromHashmap(hashmap);

        result.VehicleState.VehicleId.Should().Be("veh_456");
        result.VehicleState.Role.Should().Be("cargo");
        result.VehicleState.Index.Should().Be(3);
    }

    [Fact]
    public void FromHashmap_WithDiveState33Elements_ShouldCaptureFullState()
    {
        var hashmap = BuildMinimalPlayerHashmap();
        var diveState = new List<object> { true };
        for (var i = 1; i <= 32; i++)
        {
            diveState.Add((double)i * 0.5);
        }

        hashmap["diveState"] = diveState;

        var result = PersistencePlayerConverter.FromHashmap(hashmap);

        result.DiveState.IsDiving.Should().BeTrue();
        result.DiveState.RawData.Should().HaveCount(32);
        result.DiveState.RawData[0].Should().Be(0.5);
        result.DiveState.RawData[31].Should().Be(16.0);
    }

    [Fact]
    public void FromHashmap_WithEmptyWeaponSlots_ShouldNotError()
    {
        var hashmap = BuildMinimalPlayerHashmap();
        hashmap["loadout"] = new List<object>
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
        };

        var act = () => PersistencePlayerConverter.FromHashmap(hashmap);

        act.Should().NotThrow();
        var result = PersistencePlayerConverter.FromHashmap(hashmap);
        result.Loadout.PrimaryWeapon.Weapon.Should().BeEmpty();
        result.Loadout.SecondaryWeapon.Weapon.Should().BeEmpty();
        result.Loadout.Handgun.Weapon.Should().BeEmpty();
    }

    [Fact]
    public void FromHashmap_WithContainerItemTypes_ShouldConvertAllFourTypes()
    {
        var hashmap = BuildMinimalPlayerHashmap();

        var weaponInContainer = new List<object>
        {
            new List<object>
            {
                "arifle_MX_F",
                "",
                "",
                "",
                new List<object> { "30Rnd_65x39_caseless_mag", 30L },
                new List<object>(),
                ""
            },
            1L
        };

        hashmap["loadout"] = new List<object>
        {
            new List<object>(),
            new List<object>(),
            new List<object>(),
            new List<object>
            {
                "U_B_CombatUniform_mcam",
                new List<object>
                {
                    new List<object> { "FirstAidKit", 1L },
                    new List<object>
                    {
                        "30Rnd_65x39_caseless_mag",
                        2L,
                        30L
                    },
                    weaponInContainer,
                    new List<object> { "B_Carryall_khk", true }
                }
            },
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
        };

        var result = PersistencePlayerConverter.FromHashmap(hashmap);

        var items = result.Loadout.Uniform.Items;
        items.Should().HaveCount(4);

        items[0].Type.Should().Be("item");
        items[0].ClassName.Should().Be("FirstAidKit");
        items[0].Count.Should().Be(1);
        items[0].Ammo.Should().BeNull();
        items[0].Weapon.Should().BeNull();
        items[0].IsBackpack.Should().BeNull();

        items[1].Type.Should().Be("magazine");
        items[1].ClassName.Should().Be("30Rnd_65x39_caseless_mag");
        items[1].Count.Should().Be(2);
        items[1].Ammo.Should().Be(30);

        items[2].Type.Should().Be("weapon");
        items[2].Weapon.Should().NotBeNull();
        items[2].Weapon!.Weapon.Should().Be("arifle_MX_F");
        items[2].Count.Should().Be(1);

        items[3].Type.Should().Be("container");
        items[3].ClassName.Should().Be("B_Carryall_khk");
        items[3].IsBackpack.Should().BeTrue();
    }

    [Fact]
    public void FromHashmap_WithAceMedicalDictionary_PopulatedWounds_ArrayInner()
    {
        var hashmap = BuildMinimalPlayerHashmap();
        hashmap["aceMedical"] = new Dictionary<string, object>
        {
            ["ace_medical_bloodVolume"] = 5.5,
            ["ace_medical_openWounds"] = new Dictionary<string, object> { ["Head"] = new object[] { new object[] { 15L, 1L, 0.1, 0.5 } } }
        };

        var result = PersistencePlayerConverter.FromHashmap(hashmap);

        result.AceMedical.OpenWounds.Should().ContainKey("Head");
        result.AceMedical.OpenWounds["Head"].Should().HaveCount(1);
        result.AceMedical.OpenWounds["Head"][0].ClassComplex.Should().Be(15);
        result.AceMedical.OpenWounds["Head"][0].AmountOf.Should().Be(1);
        result.AceMedical.OpenWounds["Head"][0].BleedingRate.Should().BeApproximately(0.1, 0.0001);
        result.AceMedical.OpenWounds["Head"][0].WoundDamage.Should().BeApproximately(0.5, 0.0001);
    }

    [Fact]
    public void ToHashmap_RoundTrip_ShouldPreserveAllFields()
    {
        var original = new PlayerRedeployData
        {
            Position = [150.0, 250.0, 5.0],
            VehicleState = new PlayerVehicleState
            {
                VehicleId = "veh_rt",
                Role = "cargo",
                Index = 2
            },
            Direction = 90.0,
            Animation = "amovpercmstpsnonwnondnon",
            Loadout = new ArmaLoadout
            {
                PrimaryWeapon = new WeaponSlot
                {
                    Weapon = "arifle_MX_F",
                    Muzzle = "muzzle_snds_H",
                    PrimaryMagazine = new MagazineState { ClassName = "30Rnd_65x39_caseless_mag", Ammo = 25 }
                },
                Uniform = new ContainerSlot
                {
                    ClassName = "U_B_CombatUniform_mcam",
                    Items =
                    [
                        new ContainerItem
                        {
                            Type = "item",
                            ClassName = "FirstAidKit",
                            Count = 2
                        }
                    ]
                },
                Headgear = "H_HelmetB",
                LinkedItems = new LinkedItems
                {
                    Map = "ItemMap",
                    Gps = "ItemGPS",
                    Radio = "ItemRadio",
                    Compass = "ItemCompass",
                    Watch = "ItemWatch",
                    Nvg = "NVGoggles"
                }
            },
            Damage = 0.1,
            AceMedical = new AceMedicalState { BloodVolume = 6.0, HeartRate = 80 },
            Earplugs = true,
            AttachedItems = ["ACE_IR_Strobe_Item"],
            Radios =
            [
                new RadioState
                {
                    Type = "ACRE_PRC152",
                    Channel = 2,
                    Volume = 0.7,
                    Spatial = "LEFT",
                    PttIndex = 1
                }
            ],
            DiveState = new PlayerDiveState { IsDiving = false, RawData = [] }
        };

        var hashmap = PersistencePlayerConverter.ToHashmap(original);
        var roundTripped = PersistencePlayerConverter.FromHashmap(hashmap);

        roundTripped.Position.Should().BeEquivalentTo(original.Position);
        roundTripped.VehicleState.VehicleId.Should().Be(original.VehicleState.VehicleId);
        roundTripped.VehicleState.Role.Should().Be(original.VehicleState.Role);
        roundTripped.VehicleState.Index.Should().Be(original.VehicleState.Index);
        roundTripped.Direction.Should().Be(original.Direction);
        roundTripped.Animation.Should().Be(original.Animation);
        roundTripped.Loadout.PrimaryWeapon.Weapon.Should().Be(original.Loadout.PrimaryWeapon.Weapon);
        roundTripped.Loadout.PrimaryWeapon.Muzzle.Should().Be(original.Loadout.PrimaryWeapon.Muzzle);
        roundTripped.Loadout.PrimaryWeapon.PrimaryMagazine.ClassName.Should().Be(original.Loadout.PrimaryWeapon.PrimaryMagazine.ClassName);
        roundTripped.Loadout.PrimaryWeapon.PrimaryMagazine.Ammo.Should().Be(original.Loadout.PrimaryWeapon.PrimaryMagazine.Ammo);
        roundTripped.Loadout.Uniform.ClassName.Should().Be(original.Loadout.Uniform.ClassName);
        roundTripped.Loadout.Uniform.Items.Should().HaveCount(1);
        roundTripped.Loadout.Headgear.Should().Be(original.Loadout.Headgear);
        roundTripped.Loadout.LinkedItems.Map.Should().Be(original.Loadout.LinkedItems.Map);
        roundTripped.Loadout.LinkedItems.Nvg.Should().Be(original.Loadout.LinkedItems.Nvg);
        roundTripped.Damage.Should().Be(original.Damage);
        roundTripped.AceMedical.BloodVolume.Should().Be(original.AceMedical.BloodVolume);
        roundTripped.AceMedical.HeartRate.Should().Be(original.AceMedical.HeartRate);
        roundTripped.Earplugs.Should().Be(original.Earplugs);
        roundTripped.AttachedItems.Should().BeEquivalentTo(original.AttachedItems);
        roundTripped.Radios.Should().HaveCount(1);
        roundTripped.Radios[0].Type.Should().Be(original.Radios[0].Type);
        roundTripped.Radios[0].Channel.Should().Be(original.Radios[0].Channel);
        roundTripped.Radios[0].Volume.Should().Be(original.Radios[0].Volume);
        roundTripped.Radios[0].Spatial.Should().Be(original.Radios[0].Spatial);
        roundTripped.Radios[0].PttIndex.Should().Be(original.Radios[0].PttIndex);
        roundTripped.DiveState.IsDiving.Should().Be(original.DiveState.IsDiving);
    }

    [Fact]
    public void FromHashmap_WithAceMedicalDictionary_ShouldParseCorrectly()
    {
        var hashmap = BuildMinimalPlayerHashmap();
        hashmap["aceMedical"] = new Dictionary<string, object>
        {
            ["ace_medical_bloodVolume"] = 5.2,
            ["ace_medical_heartRate"] = 90L,
            ["ace_medical_openWounds"] = new Dictionary<string, object>(),
            ["ace_medical_pain"] = 0.3
        };

        var result = PersistencePlayerConverter.FromHashmap(hashmap);

        result.AceMedical.Should().NotBeNull();
        result.AceMedical.BloodVolume.Should().Be(5.2);
        result.AceMedical.HeartRate.Should().Be(90);
        result.AceMedical.Pain.Should().Be(0.3);
    }

    [Fact]
    public void ToHashmap_AceMedical_ShouldReturnDictionaryNotString()
    {
        var player = new PlayerRedeployData
        {
            Position = [0.0, 0.0, 0.0],
            VehicleState = new PlayerVehicleState
            {
                VehicleId = "",
                Role = "",
                Index = -1
            },
            Direction = 0.0,
            Animation = "",
            Loadout = new ArmaLoadout(),
            Damage = 0.0,
            AceMedical = new AceMedicalState { BloodVolume = 6.0, HeartRate = 80 },
            Earplugs = false,
            AttachedItems = [],
            Radios = [],
            DiveState = new PlayerDiveState { IsDiving = false, RawData = [] }
        };

        var result = PersistencePlayerConverter.ToHashmap(player);

        result["aceMedical"].Should().BeOfType<Dictionary<string, object>>();
        var medical = (Dictionary<string, object>)result["aceMedical"];
        medical["ace_medical_bloodVolume"].Should().Be(6.0);
        medical["ace_medical_heartRate"].Should().Be(80.0);
    }

    [Fact]
    public void ToHashmap_ShouldProduceCorrectKeys()
    {
        var player = new PlayerRedeployData
        {
            Position = [0.0, 0.0, 0.0],
            VehicleState = new PlayerVehicleState
            {
                VehicleId = "",
                Role = "",
                Index = -1
            },
            Direction = 0.0,
            Animation = "",
            Loadout = new ArmaLoadout(),
            Damage = 0.0,
            AceMedical = new AceMedicalState(),
            Earplugs = false,
            AttachedItems = [],
            Radios = [],
            DiveState = new PlayerDiveState { IsDiving = false, RawData = [] }
        };

        var result = PersistencePlayerConverter.ToHashmap(player);

        result.Should().HaveCount(11);
        result.Keys.Should()
              .BeEquivalentTo(
                  new[]
                  {
                      "position",
                      "vehicleState",
                      "direction",
                      "animation",
                      "loadout",
                      "damage",
                      "aceMedical",
                      "earplugs",
                      "attachedItems",
                      "radios",
                      "diveState"
                  }
              );
    }

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
                new List<object>(), // primary weapon
                new List<object>(), // secondary weapon
                new List<object>(), // handgun
                new List<object>(), // uniform
                new List<object>(), // vest
                new List<object>(), // backpack
                "", // headgear
                "", // facewear
                new List<object>(), // binoculars
                new List<object>
                {
                    "",
                    "",
                    "",
                    "",
                    "",
                    ""
                } // linked items
            },
            ["damage"] = 0.0,
            ["aceMedical"] = new Dictionary<string, object>(),
            ["earplugs"] = false,
            ["attachedItems"] = new List<object>(),
            ["radios"] = new List<object>(),
            ["diveState"] = new List<object> { false }
        };
}
