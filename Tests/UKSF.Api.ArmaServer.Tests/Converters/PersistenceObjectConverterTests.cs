using System.Collections.Generic;
using FluentAssertions;
using UKSF.Api.ArmaServer.Converters;
using UKSF.Api.ArmaServer.Models.Persistence;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Converters;

public class PersistenceObjectConverterTests
{
    [Fact]
    public void FromHashmap_WithMinimalObject_ShouldParseAllFields()
    {
        var hashmap = BuildMinimalHashmap();
        hashmap["id"] = "crate_001";
        hashmap["type"] = "B_supplyCrate_F";
        hashmap["position"] = new List<object>
        {
            1234.56,
            5678.90,
            0.05
        };
        hashmap["vectorDirUp"] = new List<object>
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
        };
        hashmap["damage"] = 0.25;
        hashmap["fuel"] = 0.0;
        hashmap["aceFortify"] = new List<object> { true, "WEST" };
        hashmap["aceMedical"] = new List<object>
        {
            1L,
            true,
            false
        };
        hashmap["customName"] = "Test Crate";

        var result = PersistenceObjectConverter.FromHashmap(hashmap);

        result.Id.Should().Be("crate_001");
        result.Type.Should().Be("B_supplyCrate_F");
        result.Position.Should().BeEquivalentTo(new[] { 1234.56, 5678.90, 0.05 });
        result.VectorDirUp.Should().HaveCount(2);
        result.VectorDirUp[0].Should().BeEquivalentTo(new[] { 0.0, 1.0, 0.0 });
        result.VectorDirUp[1].Should().BeEquivalentTo(new[] { 0.0, 0.0, 1.0 });
        result.Damage.Should().Be(0.25);
        result.Fuel.Should().Be(0.0);
        result.TurretWeapons.Should().BeEmpty();
        result.TurretMagazines.Should().BeEmpty();
        result.AceCargo.Should().BeEmpty();
        result.AceFortify.IsAceFortification.Should().BeTrue();
        result.AceFortify.Side.Should().Be("WEST");
        result.AceMedical.MedicClass.Should().Be(1);
        result.AceMedical.MedicalVehicle.Should().BeTrue();
        result.AceMedical.MedicalFacility.Should().BeFalse();
        result.CustomName.Should().Be("Test Crate");
    }

    [Fact]
    public void ToHashmap_ShouldProduceCorrectKeys()
    {
        var obj = new PersistenceObject
        {
            Id = "obj_001",
            Type = "B_Truck_01_transport_F",
            Position = [100.0, 200.0, 0.0],
            VectorDirUp = [new[] { 0.0, 1.0, 0.0 }, new[] { 0.0, 0.0, 1.0 }],
            Damage = 0.0,
            Fuel = 1.0,
            TurretWeapons = [],
            TurretMagazines = [],
            PylonLoadout = [],
            Logistics = [0.0, 0.0, 0.0],
            Attached = [],
            RackChannels = [],
            AceCargo = [],
            Inventory = new InventoryContainer(),
            AceFortify = new AceFortifyState(),
            AceMedical = new ObjectMedicalState(),
            AceRepair = new ObjectRepairState(),
            CustomName = string.Empty
        };

        var result = PersistenceObjectConverter.ToHashmap(obj);

        result.Should().HaveCount(18);
        result.Keys.Should()
              .BeEquivalentTo(
                  new[]
                  {
                      "id",
                      "type",
                      "position",
                      "vectorDirUp",
                      "damage",
                      "fuel",
                      "turretWeapons",
                      "turretMagazines",
                      "pylonLoadout",
                      "logistics",
                      "attached",
                      "rackChannels",
                      "aceCargo",
                      "inventory",
                      "aceFortify",
                      "aceMedical",
                      "aceRepair",
                      "customName"
                  }
              );
    }

    [Fact]
    public void RoundTrip_ShouldPreserveData()
    {
        var original = new PersistenceObject
        {
            Id = "roundtrip_001",
            Type = "B_Truck_01_transport_F",
            Position = [100.5, 200.3, 0.1],
            VectorDirUp = [new[] { 0.0, 1.0, 0.0 }, new[] { 0.0, 0.0, 1.0 }],
            Damage = 0.5,
            Fuel = 0.75,
            TurretWeapons =
            [
                new TurretWeaponsEntry { TurretPath = [0], Weapons = ["LMG_Mk200_F"] }
            ],
            TurretMagazines =
            [
                new TurretMagazineEntry
                {
                    ClassName = "200Rnd_65x39_cased_Box",
                    TurretPath = [0],
                    AmmoCount = 150
                }
            ],
            PylonLoadout = [],
            Logistics = [200.0, 50.0, 100.0],
            Attached = [],
            RackChannels = [],
            AceCargo = [],
            Inventory = new InventoryContainer(),
            AceFortify = new AceFortifyState { IsAceFortification = false, Side = "EAST" },
            AceMedical = new ObjectMedicalState
            {
                MedicClass = 2,
                MedicalVehicle = true,
                MedicalFacility = true
            },
            AceRepair = new ObjectRepairState { RepairVehicle = 1, RepairFacility = 0 },
            CustomName = "Supply Truck Alpha"
        };

        var hashmap = PersistenceObjectConverter.ToHashmap(original);
        var roundTripped = PersistenceObjectConverter.FromHashmap(hashmap);

        roundTripped.Id.Should().Be(original.Id);
        roundTripped.Type.Should().Be(original.Type);
        roundTripped.Position.Should().BeEquivalentTo(original.Position);
        roundTripped.VectorDirUp.Should().HaveCount(2);
        roundTripped.VectorDirUp[0].Should().BeEquivalentTo(original.VectorDirUp[0]);
        roundTripped.VectorDirUp[1].Should().BeEquivalentTo(original.VectorDirUp[1]);
        roundTripped.Damage.Should().Be(original.Damage);
        roundTripped.Fuel.Should().Be(original.Fuel);
        roundTripped.TurretWeapons.Should().HaveCount(1);
        roundTripped.TurretWeapons[0].TurretPath.Should().BeEquivalentTo(original.TurretWeapons[0].TurretPath);
        roundTripped.TurretWeapons[0].Weapons.Should().BeEquivalentTo(original.TurretWeapons[0].Weapons);
        roundTripped.TurretMagazines.Should().HaveCount(1);
        roundTripped.TurretMagazines[0].ClassName.Should().Be(original.TurretMagazines[0].ClassName);
        roundTripped.TurretMagazines[0].AmmoCount.Should().Be(original.TurretMagazines[0].AmmoCount);
        roundTripped.Logistics.Should().BeEquivalentTo(original.Logistics);
        roundTripped.AceFortify.IsAceFortification.Should().Be(original.AceFortify.IsAceFortification);
        roundTripped.AceFortify.Side.Should().Be(original.AceFortify.Side);
        roundTripped.AceMedical.MedicClass.Should().Be(original.AceMedical.MedicClass);
        roundTripped.AceMedical.MedicalVehicle.Should().Be(original.AceMedical.MedicalVehicle);
        roundTripped.AceMedical.MedicalFacility.Should().Be(original.AceMedical.MedicalFacility);
        roundTripped.AceRepair.RepairVehicle.Should().Be(original.AceRepair.RepairVehicle);
        roundTripped.AceRepair.RepairFacility.Should().Be(original.AceRepair.RepairFacility);
        roundTripped.CustomName.Should().Be(original.CustomName);
    }

    [Fact]
    public void FromHashmap_WithTurretData_ShouldParseSubArrays()
    {
        var hashmap = BuildMinimalHashmap();
        hashmap["id"] = "vehicle_001";
        hashmap["type"] = "B_MRAP_01_hmg_F";
        hashmap["turretWeapons"] = new List<object> { new List<object> { new List<object> { 0L }, new List<object> { "HMG_127", "SmokeLauncher" } } };
        hashmap["turretMagazines"] = new List<object>
        {
            new List<object>
            {
                "100Rnd_127x99_mag",
                new List<object> { 0L },
                100L
            },
            new List<object>
            {
                "SmokeLauncherMag",
                new List<object> { 0L },
                2L
            }
        };

        var result = PersistenceObjectConverter.FromHashmap(hashmap);

        result.TurretWeapons.Should().HaveCount(1);
        result.TurretWeapons[0].TurretPath.Should().BeEquivalentTo(new[] { 0 });
        result.TurretWeapons[0].Weapons.Should().BeEquivalentTo(new[] { "HMG_127", "SmokeLauncher" });

        result.TurretMagazines.Should().HaveCount(2);
        result.TurretMagazines[0].ClassName.Should().Be("100Rnd_127x99_mag");
        result.TurretMagazines[0].TurretPath.Should().BeEquivalentTo(new[] { 0 });
        result.TurretMagazines[0].AmmoCount.Should().Be(100);
        result.TurretMagazines[1].ClassName.Should().Be("SmokeLauncherMag");
        result.TurretMagazines[1].AmmoCount.Should().Be(2);
    }

    private static Dictionary<string, object> BuildMinimalHashmap() =>
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
}
