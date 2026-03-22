using System.Collections.Generic;
using FluentAssertions;
using UKSF.Api.ArmaServer.Converters;
using UKSF.Api.ArmaServer.Models.Persistence;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Converters;

public class PersistenceObjectConverterTests
{
    [Fact]
    public void FromArray_WithMinimalObject_ShouldConvertAllFields()
    {
        var array = BuildMinimalObjectArray();
        array[0] = "crate_001";
        array[1] = "B_supplyCrate_F";
        array[2] = new List<object>
        {
            1234.56,
            5678.90,
            0.05
        };
        array[3] = new List<object>
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
        array[4] = 0.25;
        array[5] = 0.0;
        array[14] = new List<object> { true, "WEST" };
        array[15] = new List<object>
        {
            1L,
            true,
            false
        };

        var result = PersistenceObjectConverter.FromArray(array);

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
    }

    [Fact]
    public void FromArray_WithTurretsAndInventory_ShouldConvertNestedStructures()
    {
        var array = BuildMinimalObjectArray();
        array[0] = "vehicle_001";
        array[1] = "B_MRAP_01_hmg_F";

        // Turret weapons: [[turretPath, [weapons...]], ...]
        array[6] = new List<object> { new List<object> { new List<object> { 0L }, new List<object> { "HMG_127", "SmokeLauncher" } } };

        // Turret magazines from magazinesAllTurrets: 5-element arrays, only first 3 consumed
        // Format: [className, turretPath, ammoCount, ?, ?]
        array[7] = new List<object>
        {
            new List<object>
            {
                "100Rnd_127x99_mag",
                new List<object> { 0L },
                100L,
                0L,
                0L
            },
            new List<object>
            {
                "SmokeLauncherMag",
                new List<object> { 0L },
                2L,
                0L,
                0L
            }
        };

        // Inventory: [[classNames[], counts[]], [classNames[], counts[]], [classNames[], counts[]], [classNames[], counts[]]]
        // Order: weapons, magazines, items, backpacks
        array[13] = new List<object>
        {
            new List<object> { new List<object>(), new List<object>() },
            new List<object> { new List<object>(), new List<object>() },
            new List<object> { new List<object> { "FirstAidKit", "Medikit" }, new List<object> { 10L, 2L } },
            new List<object> { new List<object>(), new List<object>() }
        };

        var result = PersistenceObjectConverter.FromArray(array);

        result.TurretWeapons.Should().HaveCount(1);
        result.TurretWeapons[0].TurretPath.Should().BeEquivalentTo(new[] { 0 });
        result.TurretWeapons[0].Weapons.Should().BeEquivalentTo(new[] { "HMG_127", "SmokeLauncher" });

        result.TurretMagazines.Should().HaveCount(2);
        result.TurretMagazines[0].ClassName.Should().Be("100Rnd_127x99_mag");
        result.TurretMagazines[0].TurretPath.Should().BeEquivalentTo(new[] { 0 });
        result.TurretMagazines[0].AmmoCount.Should().Be(100);
        result.TurretMagazines[1].ClassName.Should().Be("SmokeLauncherMag");
        result.TurretMagazines[1].AmmoCount.Should().Be(2);

        result.Inventory.Items.ClassNames.Should().BeEquivalentTo(new[] { "FirstAidKit", "Medikit" });
        result.Inventory.Items.Counts.Should().BeEquivalentTo(new[] { 10, 2 });
        result.Inventory.Weapons.ClassNames.Should().BeEmpty();
        result.Inventory.Backpacks.ClassNames.Should().BeEmpty();
    }

    [Fact]
    public void ToArray_ShouldProduceArrayThatRoundTripsBackToSameObject()
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
            Logistics = [200.0, 50.0, 100.0],
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

        var array = PersistenceObjectConverter.ToArray(original);
        var roundTripped = PersistenceObjectConverter.FromArray(array);

        roundTripped.Id.Should().Be(original.Id);
        roundTripped.Type.Should().Be(original.Type);
        roundTripped.Position.Should().BeEquivalentTo(original.Position);
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
        roundTripped.AceRepair.RepairVehicle.Should().Be(original.AceRepair.RepairVehicle);
        roundTripped.CustomName.Should().Be(original.CustomName);
    }

    [Fact]
    public void FromArray_WithAceCargo_ShouldConvertRecursiveStructure()
    {
        var array = BuildMinimalObjectArray();
        array[0] = "crate_002";
        array[1] = "B_CargoNet_01_ammo_F";

        // ACE cargo format: [className, nestedCargo[], inventory, customName]
        // Entry 1: Simple — ACE_Wheel with no nested cargo or inventory
        var simpleCargoEntry = new List<object>
        {
            "ACE_Wheel",
            new List<object>(),
            new List<object>
            {
                new List<object> { new List<object>(), new List<object>() },
                new List<object> { new List<object>(), new List<object>() },
                new List<object> { new List<object>(), new List<object>() },
                new List<object> { new List<object>(), new List<object>() }
            },
            ""
        };

        // Entry 2: With inventory containing magazines
        var cargoWithInventory = new List<object>
        {
            "B_supplyCrate_F",
            new List<object>(),
            new List<object>
            {
                new List<object> { new List<object>(), new List<object>() },
                new List<object> { new List<object> { "30Rnd_65x39_caseless_mag", "16Rnd_9x21_Mag" }, new List<object> { 5L, 3L } },
                new List<object> { new List<object>(), new List<object>() },
                new List<object> { new List<object>(), new List<object>() }
            },
            "Ammo Crate"
        };

        array[12] = new List<object> { simpleCargoEntry, cargoWithInventory };

        var result = PersistenceObjectConverter.FromArray(array);

        result.AceCargo.Should().HaveCount(2);

        result.AceCargo[0].ClassName.Should().Be("ACE_Wheel");
        result.AceCargo[0].Cargo.Should().BeEmpty();
        result.AceCargo[0].Inventory.Magazines.ClassNames.Should().BeEmpty();
        result.AceCargo[0].CustomName.Should().BeEmpty();

        result.AceCargo[1].ClassName.Should().Be("B_supplyCrate_F");
        result.AceCargo[1].Cargo.Should().BeEmpty();
        result.AceCargo[1].Inventory.Magazines.ClassNames.Should().BeEquivalentTo(new[] { "30Rnd_65x39_caseless_mag", "16Rnd_9x21_Mag" });
        result.AceCargo[1].Inventory.Magazines.Counts.Should().BeEquivalentTo(new[] { 5, 3 });
        result.AceCargo[1].CustomName.Should().Be("Ammo Crate");
    }

    [Fact]
    public void FromArray_With19Elements_ShouldIgnoreIndex18()
    {
        var array = BuildMinimalObjectArray();
        array[0] = "obj_003";
        array[1] = "B_Heli_Light_01_F";

        // Add the 19th element (index 18 = FailedLastLoad, runtime-only flag)
        array.Add(true);

        array.Should().HaveCount(19);

        var result = PersistenceObjectConverter.FromArray(array);

        result.Id.Should().Be("obj_003");
        result.Type.Should().Be("B_Heli_Light_01_F");
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
            new List<object> // 13: inventory (4 cargo slots, all empty)
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
}
