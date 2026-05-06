using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models.Persistence;
using UKSF.Api.ArmaServer.Parsing;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class PersistenceSessionsServiceTests
{
    private readonly Mock<IPersistenceSessionsContext> _mockContext = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();

    private readonly PersistenceSessionsService _subject;

    public PersistenceSessionsServiceTests()
    {
        _subject = new PersistenceSessionsService(_mockContext.Object, _mockLogger.Object);
    }

    private static Dictionary<string, object> JsonToDict(string json)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new PersistenceTypeConverter() } };
        return JsonSerializer.Deserialize<Dictionary<string, object>>(json, options) ?? new();
    }

    private static Dictionary<string, object> SqfToDict(string sqf) =>
        UKSF.Api.ArmaServer.Converters.PersistenceConversionHelpers.ToDict(SqfNotationParser.ParseAndNormalize(sqf));

    [Fact]
    public void Load_WithExistingKey_ReturnsSession()
    {
        var session = new DomainPersistenceSession { Key = "test-key" };
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainPersistenceSession, bool>>())).Returns(session);

        var result = _subject.Load("test-key");

        result.Should().NotBeNull();
        result!.Key.Should().Be("test-key");
    }

    [Fact]
    public void Load_WithMissingKey_ReturnsNull()
    {
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainPersistenceSession, bool>>())).Returns((DomainPersistenceSession)null);

        var result = _subject.Load("nonexistent-key");

        result.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_WithNewKey_CreatesSession()
    {
        var session = new DomainPersistenceSession { Key = "new-key" };
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainPersistenceSession, bool>>())).Returns((DomainPersistenceSession)null);

        await _subject.SaveAsync("new-key", session);

        _mockContext.Verify(x => x.Add(It.Is<DomainPersistenceSession>(s => s.Key == "new-key")), Times.Once);
        _mockContext.Verify(x => x.Replace(It.IsAny<DomainPersistenceSession>()), Times.Never);
    }

    [Fact]
    public async Task SaveAsync_WithExistingKey_ReplacesSession()
    {
        var existingSession = new DomainPersistenceSession { Id = "existing-id", Key = "existing-key" };
        var newSession = new DomainPersistenceSession { Key = "existing-key" };
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainPersistenceSession, bool>>())).Returns(existingSession);

        await _subject.SaveAsync("existing-key", newSession);

        _mockContext.Verify(x => x.Replace(It.Is<DomainPersistenceSession>(s => s.Key == "existing-key" && s.Id == "existing-id")), Times.Once);
        _mockContext.Verify(x => x.Add(It.IsAny<DomainPersistenceSession>()), Times.Never);
    }

    [Fact]
    public async Task SaveAsync_SetsSavedAtToUtcNow()
    {
        var session = new DomainPersistenceSession { Key = "key" };
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainPersistenceSession, bool>>())).Returns((DomainPersistenceSession)null);

        var before = DateTime.UtcNow;
        await _subject.SaveAsync("key", session);
        var after = DateTime.UtcNow;

        _mockContext.Verify(x => x.Add(It.Is<DomainPersistenceSession>(s => s.SavedAt >= before && s.SavedAt <= after)), Times.Once);
    }

    [Fact]
    public async Task HandleSaveAsync_DeserialisesAndSavesSession()
    {
        var sessionData = new Dictionary<string, object>
        {
            ["objects"] = new List<object>(),
            ["players"] = new Dictionary<string, object>(),
            ["mapMarkers"] = new List<object>()
        };

        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainPersistenceSession, bool>>())).Returns((DomainPersistenceSession)null);

        await _subject.HandleSaveAsync("save-key", string.Empty, sessionData);

        _mockContext.Verify(x => x.Add(It.Is<DomainPersistenceSession>(s => s.Key == "save-key")), Times.Once);
    }

    [Fact]
    public async Task HandleSaveAsync_PreservesObjectAndPlayerData()
    {
        const string json = """
                            {
                                "objects": [
                                    {"id":"obj-1","type":"B_MRAP_01_F","position":[100.5,200.3,0.1],"vectorDirUp":[[0,1,0],[0,0,1]],"damage":0.25,"fuel":0.8,"turretWeapons":[],"turretMagazines":[],"pylonLoadout":[],"logistics":[],"attached":[],"rackChannels":[],"aceCargo":[],"inventory":[[[],[]],[[],[]],[[],[]],[[],[]]],"aceFortify":[false,""],"aceMedical":[0,false,false],"aceRepair":[0,0],"customName":""}
                                ],
                                "players": {"player-uid": {"position":[50.0,60.0,0.0],"direction":180.0,"animation":"AmovPercMstpSnonWnonDnon","vehicleState":["","",-1],"loadout":[],"damage":0,"aceMedical":[],"earplugs":false,"attachedItems":[],"radios":[],"diveState":[false]}},
                                "mapMarkers": []
                            }
                            """;
        var sessionData = JsonToDict(json);

        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainPersistenceSession, bool>>())).Returns((DomainPersistenceSession)null);

        DomainPersistenceSession savedSession = null;
        _mockContext.Setup(x => x.Add(It.IsAny<DomainPersistenceSession>()))
                    .Callback<DomainPersistenceSession>(s => savedSession = s)
                    .Returns(Task.CompletedTask);

        await _subject.HandleSaveAsync("preserve-key", string.Empty, sessionData);

        savedSession.Should().NotBeNull();
        savedSession!.Objects.Should().HaveCount(1);
        savedSession.Objects[0].Id.Should().Be("obj-1");
        savedSession.Objects[0].Type.Should().Be("B_MRAP_01_F");
        savedSession.Objects[0].Position.Should().BeEquivalentTo(new[] { 100.5, 200.3, 0.1 });
        savedSession.Players.Should().ContainKey("player-uid");
        savedSession.Players["player-uid"].Animation.Should().Be("AmovPercMstpSnonWnonDnon");
    }

    [Fact]
    public async Task HandleSaveAsync_WithEmptyData_DoesNotSave()
    {
        await _subject.HandleSaveAsync("bad-key", string.Empty, new Dictionary<string, object>());

        _mockContext.Verify(x => x.Add(It.IsAny<DomainPersistenceSession>()), Times.Never);
        _mockContext.Verify(x => x.Replace(It.IsAny<DomainPersistenceSession>()), Times.Never);
    }

    [Fact]
    public void RoundTrip_ObjectWithAllFields_PreservesData()
    {
        var original = new PersistenceObject
        {
            Id = "obj-all-fields",
            Type = "B_Heli_Transport_01_F",
            Position = [1234.56, 5678.90, 15.3],
            VectorDirUp = [new double[] { 0.0, 0.8, 0.0 }, new double[] { 0.0, 0.0, 1.0 }],
            Damage = 0.35,
            Fuel = 0.72,
            TurretWeapons =
            [
                new TurretWeaponsEntry { TurretPath = [0, 1], Weapons = ["LMG_Minigun", "missiles_DAR"] }
            ],
            TurretMagazines =
            [
                new TurretMagazineEntry
                {
                    ClassName = "200Rnd_65x39_cased_Box",
                    TurretPath = [0],
                    AmmoCount = 200
                }
            ],
            PylonLoadout =
            [
                new PylonEntry { Magazine = "PylonMissile_1Rnd_Missile_AA_04_F", Ammo = 1 },
                new PylonEntry { Magazine = "PylonRack_12Rnd_missiles", Ammo = 12 }
            ],
            Logistics = [100.0, 50.0, 75.0, 200.0],
            Attached =
            [
                new AttachedObject { ClassName = "ACE_IR_Strobe_Effect", Offset = [0.0, 0.0, 1.5] }
            ],
            RackChannels = [0, 1, 2],
            AceCargo =
            [
                new AceCargoEntry { ClassName = "Box_IND_Ammo_F", CustomName = "Ammo Box" }
            ],
            Inventory =
                new InventoryContainer
                {
                    Weapons = new CargoSlot { ClassNames = ["item1"], Counts = [1] }, Magazines = new CargoSlot { ClassNames = ["mag1"], Counts = [10] }
                },
            AceFortify = new AceFortifyState { IsAceFortification = false, Side = "WEST" },
            AceMedical = new ObjectMedicalState
            {
                MedicClass = 0,
                MedicalVehicle = false,
                MedicalFacility = false
            },
            AceRepair = new ObjectRepairState { RepairVehicle = 0, RepairFacility = 0 },
            CustomName = "Custom Helicopter"
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<PersistenceObject>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Id.Should().Be(original.Id);
        deserialized.Type.Should().Be(original.Type);
        deserialized.Position.Should().BeEquivalentTo(original.Position);
        deserialized.VectorDirUp.Should().BeEquivalentTo(original.VectorDirUp);
        deserialized.Damage.Should().Be(original.Damage);
        deserialized.Fuel.Should().Be(original.Fuel);
        deserialized.CustomName.Should().Be(original.CustomName);
        deserialized.Logistics.Should().BeEquivalentTo(original.Logistics);

        deserialized.TurretWeapons.Should().HaveCount(1);
        deserialized.TurretWeapons[0].TurretPath.Should().BeEquivalentTo(new[] { 0, 1 });
        deserialized.TurretWeapons[0].Weapons.Should().BeEquivalentTo(new[] { "LMG_Minigun", "missiles_DAR" });

        deserialized.TurretMagazines.Should().HaveCount(1);
        deserialized.TurretMagazines[0].ClassName.Should().Be("200Rnd_65x39_cased_Box");
        deserialized.TurretMagazines[0].TurretPath.Should().BeEquivalentTo(new[] { 0 });

        deserialized.PylonLoadout.Should().HaveCount(2);
        deserialized.PylonLoadout[0].Magazine.Should().Be("PylonMissile_1Rnd_Missile_AA_04_F");
        deserialized.PylonLoadout[0].Ammo.Should().Be(1);

        deserialized.Attached.Should().HaveCount(1);
        deserialized.Attached[0].ClassName.Should().Be("ACE_IR_Strobe_Effect");
        deserialized.Attached[0].Offset.Should().BeEquivalentTo(new[] { 0.0, 0.0, 1.5 });

        deserialized.RackChannels.Should().BeEquivalentTo(new[] { 0, 1, 2 });

        deserialized.AceCargo.Should().HaveCount(1);
        deserialized.AceCargo[0].ClassName.Should().Be("Box_IND_Ammo_F");
        deserialized.AceCargo[0].CustomName.Should().Be("Ammo Box");

        deserialized.Inventory.Weapons.ClassNames.Should().BeEquivalentTo(new[] { "item1" });
        deserialized.Inventory.Magazines.ClassNames.Should().BeEquivalentTo(new[] { "mag1" });

        deserialized.AceFortify.IsAceFortification.Should().BeFalse();
        deserialized.AceFortify.Side.Should().Be("WEST");

        deserialized.AceMedical.MedicClass.Should().Be(0);
        deserialized.AceMedical.MedicalVehicle.Should().BeFalse();

        deserialized.AceRepair.RepairVehicle.Should().Be(0);
        deserialized.AceRepair.RepairFacility.Should().Be(0);
    }

    [Fact]
    public void RoundTrip_ObjectWithDefaults_PreservesData()
    {
        var original = new PersistenceObject();

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<PersistenceObject>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Id.Should().Be(string.Empty);
        deserialized.Type.Should().Be(string.Empty);
        deserialized.Position.Should().BeEmpty();
        deserialized.VectorDirUp.Should().BeEmpty();
        deserialized.Damage.Should().Be(0);
        deserialized.Fuel.Should().Be(0);
        deserialized.TurretWeapons.Should().BeEmpty();
        deserialized.TurretMagazines.Should().BeEmpty();
        deserialized.PylonLoadout.Should().BeEmpty();
        deserialized.Logistics.Should().BeEmpty();
        deserialized.Attached.Should().BeEmpty();
        deserialized.RackChannels.Should().BeEmpty();
        deserialized.AceCargo.Should().BeEmpty();
        deserialized.Inventory.Should().NotBeNull();
        deserialized.AceFortify.Should().NotBeNull();
        deserialized.AceMedical.Should().NotBeNull();
        deserialized.AceRepair.Should().NotBeNull();
        deserialized.CustomName.Should().Be(string.Empty);
    }

    [Fact]
    public void RoundTrip_ObjectWithEmptyCollections_PreservesData()
    {
        var original = new PersistenceObject
        {
            Id = "empty-collections",
            Type = "B_Truck_01_transport_F",
            Position = [],
            VectorDirUp = [],
            TurretWeapons = [],
            TurretMagazines = [],
            PylonLoadout = [],
            Logistics = [],
            Attached = [],
            RackChannels = [],
            AceCargo = [],
            Inventory = new InventoryContainer(),
            AceFortify = new AceFortifyState(),
            AceMedical = new ObjectMedicalState(),
            AceRepair = new ObjectRepairState()
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<PersistenceObject>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Id.Should().Be("empty-collections");
        deserialized.Position.Should().BeEmpty();
        deserialized.VectorDirUp.Should().BeEmpty();
        deserialized.TurretWeapons.Should().BeEmpty();
        deserialized.TurretMagazines.Should().BeEmpty();
        deserialized.PylonLoadout.Should().BeEmpty();
        deserialized.Logistics.Should().BeEmpty();
        deserialized.Attached.Should().BeEmpty();
        deserialized.RackChannels.Should().BeEmpty();
        deserialized.AceCargo.Should().BeEmpty();
    }

    [Fact]
    public void RoundTrip_ObjectWithNestedAceCargo_PreservesData()
    {
        var original = new PersistenceObject
        {
            Id = "nested-cargo",
            Type = "B_CargoNet_01_ammo_F",
            AceCargo =
            [
                new AceCargoEntry
                {
                    ClassName = "Box_IND_Ammo_F",
                    Cargo = [new AceCargoEntry { ClassName = "nested_item_1" }],
                    CustomName = "Ammo Box"
                }
            ]
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<PersistenceObject>(json);

        deserialized.Should().NotBeNull();
        deserialized!.AceCargo.Should().HaveCount(1);
        deserialized.AceCargo[0].ClassName.Should().Be("Box_IND_Ammo_F");
        deserialized.AceCargo[0].Cargo.Should().HaveCount(1);
        deserialized.AceCargo[0].Cargo[0].ClassName.Should().Be("nested_item_1");
        deserialized.AceCargo[0].CustomName.Should().Be("Ammo Box");
    }

    [Fact]
    public void RoundTrip_ObjectWithTurretWeapons_PreservesData()
    {
        var original = new PersistenceObject
        {
            Id = "turret-weapons",
            Type = "B_APC_Tracked_01_AA_F",
            TurretWeapons =
            [
                new TurretWeaponsEntry { TurretPath = [0, 1], Weapons = ["weapon1", "weapon2"] }
            ]
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<PersistenceObject>(json);

        deserialized.Should().NotBeNull();
        deserialized!.TurretWeapons.Should().HaveCount(1);
        deserialized.TurretWeapons[0].TurretPath.Should().BeEquivalentTo(new[] { 0, 1 });
        deserialized.TurretWeapons[0].Weapons.Should().BeEquivalentTo(new[] { "weapon1", "weapon2" });
    }

    [Fact]
    public void RoundTrip_ObjectWithPylonLoadout_PreservesData()
    {
        var original = new PersistenceObject
        {
            Id = "pylon-loadout",
            Type = "B_Plane_CAS_01_dynamicLoadout_F",
            PylonLoadout =
            [
                new PylonEntry { Magazine = "mag1", Ammo = 20 },
                new PylonEntry { Magazine = "mag2", Ammo = 0 }
            ]
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<PersistenceObject>(json);

        deserialized.Should().NotBeNull();
        deserialized!.PylonLoadout.Should().HaveCount(2);
        deserialized.PylonLoadout[0].Magazine.Should().Be("mag1");
        deserialized.PylonLoadout[0].Ammo.Should().Be(20);
        deserialized.PylonLoadout[1].Magazine.Should().Be("mag2");
        deserialized.PylonLoadout[1].Ammo.Should().Be(0);
    }

    [Fact]
    public void RoundTrip_ObjectWithAttachedObjects_PreservesData()
    {
        var original = new PersistenceObject
        {
            Id = "attached-objects",
            Type = "B_MRAP_01_F",
            Attached =
            [
                new AttachedObject { ClassName = "classname", Offset = [0.1, 0.2, 0.3] }
            ]
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<PersistenceObject>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Attached.Should().HaveCount(1);
        deserialized.Attached[0].ClassName.Should().Be("classname");
        deserialized.Attached[0].Offset.Should().BeEquivalentTo(new[] { 0.1, 0.2, 0.3 });
    }

    [Fact]
    public void RoundTrip_ObjectWithSpecialLogistics_PreservesData()
    {
        var original = new PersistenceObject
        {
            Id = "special-logistics",
            Type = "B_Truck_01_ammo_F",
            Logistics = [0.0, -1.0, 999999.99, 42.0]
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<PersistenceObject>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Logistics.Should().BeEquivalentTo(original.Logistics);
    }

    [Fact]
    public void RoundTrip_PlayerWithAllFields_PreservesData()
    {
        var original = new PlayerRedeployData
        {
            Position = [1000.0, 2000.0, 5.5],
            VehicleState = new PlayerVehicleState
            {
                VehicleId = "persistence_id_123",
                Role = "driver",
                Index = -1
            },
            Direction = 270.0,
            Animation = "AmovPercMstpSlowWrflDnon",
            Loadout =
                new ArmaLoadout
                {
                    PrimaryWeapon = new WeaponSlot { Weapon = "arifle_MX_F" }, Uniform = new ContainerSlot { ClassName = "U_B_CombatUniform_mcam" }
                },
            Damage = 0.15,
            AceMedical = new AceMedicalState
            {
                BloodVolume = 5.5,
                HeartRate = 90.0,
                InPain = true
            },
            Earplugs = true,
            AttachedItems = ["NVGoggles", "ItemMap", "ItemCompass"],
            Radios =
            [
                new RadioState
                {
                    Type = "ACRE_PRC152",
                    Channel = 1,
                    Volume = 1.0,
                    Spatial = "CENTER"
                }
            ],
            DiveState = new PlayerDiveState { IsDiving = false }
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<PlayerRedeployData>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Position.Should().BeEquivalentTo(original.Position);
        deserialized.Direction.Should().Be(original.Direction);
        deserialized.Animation.Should().Be(original.Animation);
        deserialized.Damage.Should().Be(original.Damage);
        deserialized.Earplugs.Should().Be(original.Earplugs);
        deserialized.AttachedItems.Should().BeEquivalentTo(original.AttachedItems);

        deserialized.VehicleState.VehicleId.Should().Be("persistence_id_123");
        deserialized.VehicleState.Role.Should().Be("driver");
        ((JsonElement)deserialized.VehicleState.Index).GetInt32().Should().Be(-1);

        deserialized.Loadout.PrimaryWeapon.Weapon.Should().Be("arifle_MX_F");
        deserialized.Loadout.Uniform.ClassName.Should().Be("U_B_CombatUniform_mcam");

        deserialized.AceMedical.BloodVolume.Should().Be(original.AceMedical.BloodVolume);
        deserialized.AceMedical.HeartRate.Should().Be(original.AceMedical.HeartRate);
        deserialized.AceMedical.InPain.Should().Be(original.AceMedical.InPain);

        deserialized.Radios.Should().HaveCount(1);
        deserialized.Radios[0].Type.Should().Be("ACRE_PRC152");
        deserialized.Radios[0].Channel.Should().Be(1);

        deserialized.DiveState.IsDiving.Should().BeFalse();
    }

    [Fact]
    public void RoundTrip_PlayerOnFoot_PreservesData()
    {
        var original = new PlayerRedeployData
        {
            Position = [500.0, 600.0, 0.0],
            VehicleState = new PlayerVehicleState
            {
                VehicleId = string.Empty,
                Role = string.Empty,
                Index = -1
            },
            Direction = 90.0,
            Animation = "AmovPercMstpSnonWnonDnon"
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<PlayerRedeployData>(json);

        deserialized.Should().NotBeNull();
        deserialized!.VehicleState.VehicleId.Should().Be(string.Empty);
        deserialized.VehicleState.Role.Should().Be(string.Empty);
        ((JsonElement)deserialized.VehicleState.Index).GetInt32().Should().Be(-1);
    }

    [Fact]
    public void RoundTrip_PlayerInVehicle_PreservesData()
    {
        var original = new PlayerRedeployData
        {
            Position = [800.0, 900.0, 2.0],
            VehicleState = new PlayerVehicleState
            {
                VehicleId = "persistence_id",
                Role = "driver",
                Index = -1
            },
            Direction = 45.0
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<PlayerRedeployData>(json);

        deserialized.Should().NotBeNull();
        deserialized!.VehicleState.VehicleId.Should().Be("persistence_id");
        deserialized.VehicleState.Role.Should().Be("driver");
        ((JsonElement)deserialized.VehicleState.Index).GetInt32().Should().Be(-1);
    }

    [Fact]
    public void RoundTrip_PlayerInTurret_PreservesData()
    {
        var original = new PlayerRedeployData
        {
            Position = [800.0, 900.0, 2.0],
            VehicleState = new PlayerVehicleState
            {
                VehicleId = "persistence_id",
                Role = "turret",
                Index = 0
            },
            Direction = 180.0
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<PlayerRedeployData>(json);

        deserialized.Should().NotBeNull();
        deserialized!.VehicleState.VehicleId.Should().Be("persistence_id");
        deserialized.VehicleState.Role.Should().Be("turret");
        ((JsonElement)deserialized.VehicleState.Index).GetInt32().Should().Be(0);
    }

    [Fact]
    public void RoundTrip_PlayerWithEmptyOptionalFields_PreservesData()
    {
        var original = new PlayerRedeployData();

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<PlayerRedeployData>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Position.Should().BeEmpty();
        deserialized.VehicleState.Should().NotBeNull();
        deserialized.VehicleState.VehicleId.Should().Be(string.Empty);
        deserialized.Direction.Should().Be(0);
        deserialized.Animation.Should().Be(string.Empty);
        deserialized.Loadout.Should().NotBeNull();
        deserialized.Damage.Should().Be(0);
        deserialized.AceMedical.Should().NotBeNull();
        deserialized.AceMedical.BloodVolume.Should().Be(0);
        deserialized.Earplugs.Should().BeFalse();
        deserialized.AttachedItems.Should().BeEmpty();
        deserialized.Radios.Should().BeEmpty();
        deserialized.DiveState.Should().NotBeNull();
        deserialized.DiveState.IsDiving.Should().BeFalse();
    }

    [Fact]
    public void RoundTrip_PlayerWithRadios_PreservesData()
    {
        var original = new PlayerRedeployData
        {
            Radios =
            [
                new RadioState
                {
                    Type = "ACRE_PRC343",
                    Channel = 8,
                    Volume = 0.8,
                    Spatial = "CENTER",
                    PttIndex = 0
                }
            ]
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<PlayerRedeployData>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Radios.Should().HaveCount(1);
        deserialized.Radios[0].Type.Should().Be("ACRE_PRC343");
        deserialized.Radios[0].Channel.Should().Be(8);
        deserialized.Radios[0].Volume.Should().Be(0.8);
        deserialized.Radios[0].Spatial.Should().Be("CENTER");
        deserialized.Radios[0].PttIndex.Should().Be(0);
    }

    [Fact]
    public void RoundTrip_StandardMarker_PreservesData()
    {
        // Standard marker: [name, type, pos, shape, color, alpha, size, dir, text, brush]
        var marker = new List<object>
        {
            "marker_1",
            "hd_dot",
            new List<object>
            {
                1000.0,
                2000.0,
                0.0
            },
            "ICON",
            "ColorRed",
            1.0,
            new List<object> { 1.0, 1.0 },
            0.0,
            "Target Alpha",
            ""
        };

        var session = new DomainPersistenceSession { Key = "marker-test", Markers = [marker] };

        var json = JsonSerializer.Serialize(session);
        var deserialized = JsonSerializer.Deserialize<DomainPersistenceSession>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Markers.Should().HaveCount(1);
        deserialized.Markers[0].Should().HaveCount(marker.Count);
        JsonSerializer.Serialize(deserialized.Markers[0]).Should().Be(JsonSerializer.Serialize(marker));
    }

    [Fact]
    public void RoundTrip_PolylineMarker_PreservesData()
    {
        // Polyline marker: [name, type, polyline, color, alpha, text]
        var marker = new List<object>
        {
            "polyline_1",
            "hd_polyline",
            new List<object>
            {
                new List<object> { 100.0, 200.0 },
                new List<object> { 300.0, 400.0 },
                new List<object> { 500.0, 600.0 }
            },
            "ColorBlue",
            0.8,
            "Route Alpha"
        };

        var session = new DomainPersistenceSession { Key = "polyline-test", Markers = [marker] };

        var json = JsonSerializer.Serialize(session);
        var deserialized = JsonSerializer.Deserialize<DomainPersistenceSession>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Markers.Should().HaveCount(1);
        JsonSerializer.Serialize(deserialized.Markers[0]).Should().Be(JsonSerializer.Serialize(marker));
    }

    [Fact]
    public void RoundTrip_MixedMarkers_PreservesData()
    {
        var standardMarker = new List<object>
        {
            "marker_1",
            "hd_dot",
            new List<object>
            {
                1000.0,
                2000.0,
                0.0
            },
            "ICON",
            "ColorRed",
            1.0,
            new List<object> { 1.0, 1.0 },
            0.0,
            "Target",
            ""
        };
        var polylineMarker = new List<object>
        {
            "polyline_1",
            "hd_polyline",
            new List<object> { new List<object> { 100.0, 200.0 }, new List<object> { 300.0, 400.0 } },
            "ColorBlue",
            0.8,
            "Route"
        };

        var session = new DomainPersistenceSession { Key = "mixed-markers", Markers = [standardMarker, polylineMarker] };

        var json = JsonSerializer.Serialize(session);
        var deserialized = JsonSerializer.Deserialize<DomainPersistenceSession>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Markers.Should().HaveCount(2);
        JsonSerializer.Serialize(deserialized.Markers).Should().Be(JsonSerializer.Serialize(session.Markers));
    }

    [Fact]
    public void RoundTrip_FullSession_PreservesData()
    {
        var original = new DomainPersistenceSession
        {
            Key = "full-session",
            Objects =
            [
                new PersistenceObject
                {
                    Id = "obj-1",
                    Type = "B_MRAP_01_F",
                    Position = [100.0, 200.0, 0.5],
                    VectorDirUp = [new[] { 1.0, 0.0, 0.0 }, new[] { 0.0, 0.0, 1.0 }],
                    Damage = 0.1,
                    Fuel = 0.9
                },
                new PersistenceObject
                {
                    Id = "obj-2",
                    Type = "B_Heli_Light_01_F",
                    Position = [300.0, 400.0, 50.0],
                    Damage = 0.0,
                    Fuel = 1.0
                }
            ],
            DeletedObjects = ["deleted-obj-1", "deleted-obj-2"],
            Players = new Dictionary<string, PlayerRedeployData>
            {
                ["uid-1"] = new()
                {
                    Position = [500.0, 600.0, 0.0],
                    VehicleState = new PlayerVehicleState
                    {
                        VehicleId = "obj-1",
                        Role = "driver",
                        Index = -1
                    },
                    Direction = 90.0,
                    Animation = "AmovPercMstpSnonWnonDnon",
                    Earplugs = true,
                    AttachedItems = ["NVGoggles"]
                },
                ["uid-2"] = new()
                {
                    Position = [700.0, 800.0, 0.0],
                    VehicleState = new PlayerVehicleState
                    {
                        VehicleId = string.Empty,
                        Role = string.Empty,
                        Index = -1
                    },
                    Direction = 180.0,
                    Animation = "AmovPercMstpSlowWrflDnon"
                }
            },
            Markers =
            [
                new List<object>
                {
                    "marker_1",
                    "hd_dot",
                    new List<object>
                    {
                        1000.0,
                        2000.0,
                        0.0
                    },
                    "ICON",
                    "ColorRed",
                    1.0,
                    new List<object> { 1.0, 1.0 },
                    0.0,
                    "Target",
                    ""
                }
            ],
            ArmaDateTime = [2035, 6, 15, 14, 30],
            CustomData = new Dictionary<string, object> { ["weather"] = "clear", ["fogLevel"] = 0.1 },
            SavedAt = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc)
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<DomainPersistenceSession>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Key.Should().Be(original.Key);
        deserialized.Objects.Should().HaveCount(2);
        deserialized.Objects[0].Id.Should().Be("obj-1");
        deserialized.Objects[1].Id.Should().Be("obj-2");
        deserialized.DeletedObjects.Should().BeEquivalentTo(original.DeletedObjects);
        deserialized.Players.Should().HaveCount(2);
        deserialized.Players.Should().ContainKey("uid-1");
        deserialized.Players["uid-1"].VehicleState.VehicleId.Should().Be("obj-1");
        deserialized.Players.Should().ContainKey("uid-2");
        deserialized.Markers.Should().HaveCount(1);
        JsonSerializer.Serialize(deserialized.Markers).Should().Be(JsonSerializer.Serialize(original.Markers));
        deserialized.ArmaDateTime.Should().BeEquivalentTo(original.ArmaDateTime);
        deserialized.SavedAt.Should().Be(original.SavedAt);

        JsonSerializer.Serialize(deserialized.CustomData).Should().Be(JsonSerializer.Serialize(original.CustomData));
    }

    [Fact]
    public void RoundTrip_EmptySession_PreservesData()
    {
        var original = new DomainPersistenceSession { Key = "empty-session" };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<DomainPersistenceSession>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Key.Should().Be("empty-session");
        deserialized.Objects.Should().BeEmpty();
        deserialized.DeletedObjects.Should().BeEmpty();
        deserialized.Players.Should().BeEmpty();
        deserialized.Markers.Should().BeEmpty();
        deserialized.ArmaDateTime.Should().BeEmpty();
        deserialized.CustomData.Should().BeEmpty();
    }

    [Fact]
    public void RoundTrip_CustomData_PreservesArbitraryStructure()
    {
        var original = new DomainPersistenceSession
        {
            Key = "custom-data-test",
            CustomData = new Dictionary<string, object>
            {
                ["stringValue"] = "hello",
                ["numberValue"] = 42,
                ["boolValue"] = true,
                ["nestedObject"] = new Dictionary<string, object> { ["inner"] = "value" },
                ["arrayValue"] = new object[] { 1, 2, 3 }
            }
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<DomainPersistenceSession>(json);

        deserialized.Should().NotBeNull();
        deserialized!.CustomData.Should().HaveCount(5);
        JsonSerializer.Serialize(deserialized.CustomData).Should().Be(JsonSerializer.Serialize(original.CustomData));
    }

    [Fact]
    public void RoundTrip_AceMedical_WithUnknownFields_PreservesAdditionalData()
    {
        var json = """
                   {
                       "ace_medical_bloodvolume": 5.5,
                       "ace_medical_heartrate": 90,
                       "ace_medical_bloodpressure": [70, 110],
                       "ace_medical_future_field": [1, 2, 3],
                       "ace_medical_another_new_thing": "test"
                   }
                   """;

        var state = JsonSerializer.Deserialize<AceMedicalState>(json);

        state.Should().NotBeNull();
        state!.BloodVolume.Should().Be(5.5);
        state.HeartRate.Should().Be(90);
        state.BloodPressure.Should().BeEquivalentTo(new[] { 70.0, 110.0 });
        state.AdditionalData.Should().ContainKey("ace_medical_future_field");
        state.AdditionalData.Should().ContainKey("ace_medical_another_new_thing");

        // Verify round-trip preserves additional data
        var reserialized = JsonSerializer.Serialize(state);
        reserialized.Should().Contain("ace_medical_future_field");
        reserialized.Should().Contain("ace_medical_another_new_thing");
    }

    [Fact]
    public void RoundTrip_AceMedicalState_WithWounds_PreservesData()
    {
        var original = new AceMedicalState
        {
            BloodVolume = 5.5,
            OpenWounds = new Dictionary<string, List<WoundEntry>>
            {
                ["0"] =
                [
                    new WoundEntry
                    {
                        ClassComplex = 1,
                        AmountOf = 2,
                        BleedingRate = 0.1,
                        WoundDamage = 0.5
                    }
                ]
            },
            Medications =
            [
                new MedicationEntry
                {
                    Medication = "Morphine",
                    TimeOffset = 0.0,
                    HrAdjust = -20.0
                }
            ],
            IvBags =
            [
                new IvBagEntry
                {
                    Volume = 500.0,
                    Type = "Saline",
                    PartIndex = 0
                }
            ],
            TriageCard =
            [
                new TriageCardEntry
                {
                    Item = "ace_triage_green",
                    Count = 1,
                    Timestamp = 100.0
                }
            ],
            Logs = [new MedicalLogCategory { LogType = "treatment", Entries = [new MedicalLogEntry { Message = "Applied bandage" }] }]
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<AceMedicalState>(json);

        deserialized.Should().NotBeNull();
        deserialized!.BloodVolume.Should().Be(5.5);
        deserialized.OpenWounds.Should().ContainKey("0");
        deserialized.OpenWounds["0"].Should().HaveCount(1);
        deserialized.OpenWounds["0"][0].ClassComplex.Should().Be(1);
        deserialized.OpenWounds["0"][0].AmountOf.Should().Be(2);
        deserialized.Medications.Should().HaveCount(1);
        deserialized.Medications[0].Medication.Should().Be("Morphine");
        deserialized.IvBags.Should().HaveCount(1);
        deserialized.IvBags[0].Volume.Should().Be(500.0);
        deserialized.TriageCard.Should().HaveCount(1);
        deserialized.TriageCard[0].Item.Should().Be("ace_triage_green");
        deserialized.Logs.Should().HaveCount(1);
        deserialized.Logs[0].LogType.Should().Be("treatment");
    }

    [Fact]
    public void RoundTrip_ArmaLoadout_WithAllSlots_PreservesData()
    {
        var original = new ArmaLoadout
        {
            PrimaryWeapon = new WeaponSlot
            {
                Weapon = "arifle_MX_F",
                Optic = "optic_MRCO",
                PrimaryMagazine = new MagazineState { ClassName = "30Rnd_65x39_caseless_mag", Ammo = 25 }
            },
            Uniform = new ContainerSlot { ClassName = "U_B_CombatUniform_mcam", Items = [new ContainerItem { ClassName = "FirstAidKit", Count = 3 }] },
            Headgear = "H_HelmetB",
            LinkedItems = new LinkedItems { Map = "ItemMap", Compass = "ItemCompass" }
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<ArmaLoadout>(json);

        deserialized.Should().NotBeNull();
        deserialized!.PrimaryWeapon.Weapon.Should().Be("arifle_MX_F");
        deserialized.PrimaryWeapon.Optic.Should().Be("optic_MRCO");
        deserialized.PrimaryWeapon.PrimaryMagazine.ClassName.Should().Be("30Rnd_65x39_caseless_mag");
        deserialized.PrimaryWeapon.PrimaryMagazine.Ammo.Should().Be(25);
        deserialized.Uniform.ClassName.Should().Be("U_B_CombatUniform_mcam");
        deserialized.Uniform.Items.Should().HaveCount(1);
        deserialized.Uniform.Items[0].ClassName.Should().Be("FirstAidKit");
        deserialized.Uniform.Items[0].Count.Should().Be(3);
        deserialized.Headgear.Should().Be("H_HelmetB");
        deserialized.LinkedItems.Map.Should().Be("ItemMap");
        deserialized.LinkedItems.Compass.Should().Be("ItemCompass");
    }

    [Fact]
    public void RoundTrip_ContainerItem_AllTypes_PreservesData()
    {
        var original = new ContainerSlot
        {
            ClassName = "B_Carryall_ocamo",
            Items =
            [
                new ContainerItem
                {
                    Type = "item",
                    ClassName = "FirstAidKit",
                    Count = 1
                },
                new ContainerItem
                {
                    Type = "magazine",
                    ClassName = "30Rnd_65x39_caseless_mag",
                    Count = 2,
                    Ammo = 30
                },
                new ContainerItem
                {
                    Type = "weapon",
                    Weapon = new WeaponSlot
                    {
                        Weapon = "arifle_MX_ACO_pointer_F",
                        Pointer = "acc_pointer_IR",
                        Optic = "optic_Aco"
                    },
                    Count = 1
                },
                new ContainerItem
                {
                    Type = "container",
                    ClassName = "B_Carryall_khk",
                    IsBackpack = true
                }
            ]
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<ContainerSlot>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Items.Should().HaveCount(4);

        deserialized.Items[0].Type.Should().Be("item");
        deserialized.Items[0].ClassName.Should().Be("FirstAidKit");
        deserialized.Items[0].Count.Should().Be(1);

        deserialized.Items[1].Type.Should().Be("magazine");
        deserialized.Items[1].ClassName.Should().Be("30Rnd_65x39_caseless_mag");
        deserialized.Items[1].Count.Should().Be(2);
        deserialized.Items[1].Ammo.Should().Be(30);

        deserialized.Items[2].Type.Should().Be("weapon");
        deserialized.Items[2].Weapon.Should().NotBeNull();
        deserialized.Items[2].Weapon!.Weapon.Should().Be("arifle_MX_ACO_pointer_F");
        deserialized.Items[2].Weapon.Pointer.Should().Be("acc_pointer_IR");
        deserialized.Items[2].Weapon.Optic.Should().Be("optic_Aco");
        deserialized.Items[2].Count.Should().Be(1);

        deserialized.Items[3].Type.Should().Be("container");
        deserialized.Items[3].ClassName.Should().Be("B_Carryall_khk");
        deserialized.Items[3].IsBackpack.Should().BeTrue();
    }

    [Fact]
    public void Deserialize_ContainerItem_WithoutTypeField_DefaultsToItem()
    {
        const string json = """{"className":"FirstAidKit","count":3}""";
        var deserialized = JsonSerializer.Deserialize<ContainerItem>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Type.Should().Be("item");
        deserialized.ClassName.Should().Be("FirstAidKit");
        deserialized.Count.Should().Be(3);
    }

    [Fact]
    public async Task HandleSaveAsync_WithHashmapFormat_SavesViaConverter()
    {
        // Build a hashmap JSON with plain keys and nested players
        const string rawJson = """
                               {
                                   "objects": [
                                       {"id":"obj-1","type":"B_MRAP_01_F","position":[100.5,200.3,0.1],"vectorDirUp":[[0,1,0],[0,0,1]],"damage":0.25,"fuel":0.8,"turretWeapons":[],"turretMagazines":[],"pylonLoadout":[],"logistics":[],"attached":[],"rackChannels":[],"aceCargo":[],"inventory":[[[],[]],[[],[]],[[],[]],[[],[]]],"aceFortify":[false,""],"aceMedical":[0,false,false],"aceRepair":[0,0],"customName":""}
                                   ],
                                   "dateTime": [2035,6,15,12,30],
                                   "deletedObjects": ["del-1"],
                                   "mapMarkers": [],
                                   "players": {}
                               }
                               """;

        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainPersistenceSession, bool>>())).Returns((DomainPersistenceSession)null);

        DomainPersistenceSession savedSession = null;
        _mockContext.Setup(x => x.Add(It.IsAny<DomainPersistenceSession>()))
                    .Callback<DomainPersistenceSession>(s => savedSession = s)
                    .Returns(Task.CompletedTask);

        await _subject.HandleSaveAsync("raw-key", "session-1", JsonToDict(rawJson));

        savedSession.Should().NotBeNull();
        savedSession!.Key.Should().Be("raw-key");
        savedSession.Objects.Should().HaveCount(1);
        savedSession.Objects[0].Id.Should().Be("obj-1");
        savedSession.Objects[0].Type.Should().Be("B_MRAP_01_F");
        savedSession.Objects[0].Position.Should().BeEquivalentTo(new[] { 100.5, 200.3, 0.1 });
        savedSession.Objects[0].Damage.Should().Be(0.25);
        savedSession.Objects[0].Fuel.Should().Be(0.8);
        savedSession.DeletedObjects.Should().ContainSingle().Which.Should().Be("del-1");
        savedSession.ArmaDateTime.Should().BeEquivalentTo(new[] { 2035, 6, 15, 12, 30 });
    }

    [Fact]
    public async Task HandleSaveAsync_WithSqfNotationPayload_SavesParsedSession()
    {
        // Engine-native str() output of a session HashMap as produced by fnc_saveDataApi.
        // HashMaps become [["k",v],...] pair-lists, identical syntax to nested arrays.
        const string sqfPayload = "[[\"dateTime\",[2026,5,2,14,18]]," +
                                  "[\"deletedObjects\",[\"obsolete-1\"]]," +
                                  "[\"mapMarkers\",[]]," +
                                  "[\"objects\",[" +
                                  "[[\"id\",\"sqf-obj-1\"]," +
                                  "[\"type\",\"B_MRAP_01_F\"]," +
                                  "[\"position\",[100.5,200.3,0.1]]," +
                                  "[\"vectorDirUp\",[[1,0,0],[0,0,1]]]," +
                                  "[\"damage\",0.25]," +
                                  "[\"fuel\",0.8]," +
                                  "[\"turretWeapons\",[]]," +
                                  "[\"turretMagazines\",[]]," +
                                  "[\"pylonLoadout\",[]]," +
                                  "[\"logistics\",[-1,-1,-1]]," +
                                  "[\"attached\",[]]," +
                                  "[\"rackChannels\",[]]," +
                                  "[\"aceCargo\",[]]," +
                                  "[\"inventory\",[[[],[]],[[],[]],[[],[]],[[],[]]]]," +
                                  "[\"aceFortify\",[false,\"west\"]]," +
                                  "[\"aceMedical\",[0,false,false]]," +
                                  "[\"aceRepair\",[0,0]]," +
                                  "[\"customName\",\"\"]]" +
                                  "]]," +
                                  "[\"players\",[[\"7656119800001\",[" +
                                  "[\"position\",[50,60,0]]," +
                                  "[\"vehicleState\",[\"\",\"\",-1]]," +
                                  "[\"direction\",180]," +
                                  "[\"animation\",\"AmovPercMstpSnonWnonDnon\"]," +
                                  "[\"loadout\",[]]," +
                                  "[\"damage\",0]," +
                                  "[\"aceMedical\",[]]," +
                                  "[\"earplugs\",false]," +
                                  "[\"attachedItems\",[]]," +
                                  "[\"radios\",[]]," +
                                  "[\"diveState\",[false]]" +
                                  "]]]]" +
                                  "]";

        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainPersistenceSession, bool>>())).Returns((DomainPersistenceSession)null);

        DomainPersistenceSession savedSession = null;
        _mockContext.Setup(x => x.Add(It.IsAny<DomainPersistenceSession>()))
                    .Callback<DomainPersistenceSession>(s => savedSession = s)
                    .Returns(Task.CompletedTask);

        await _subject.HandleSaveAsync("sqf-key", "session-id", SqfToDict(sqfPayload));

        savedSession.Should().NotBeNull();
        savedSession!.Key.Should().Be("sqf-key");
        savedSession.ArmaDateTime.Should().BeEquivalentTo(new[] { 2026, 5, 2, 14, 18 });
        savedSession.DeletedObjects.Should().ContainSingle().Which.Should().Be("obsolete-1");
        savedSession.Objects.Should().HaveCount(1);
        savedSession.Objects[0].Id.Should().Be("sqf-obj-1");
        savedSession.Objects[0].Type.Should().Be("B_MRAP_01_F");
        savedSession.Objects[0].Position.Should().BeEquivalentTo(new[] { 100.5, 200.3, 0.1 });
        savedSession.Objects[0].Damage.Should().Be(0.25);
        savedSession.Objects[0].Fuel.Should().Be(0.8);
        savedSession.Players.Should().ContainKey("7656119800001");
        savedSession.Players["7656119800001"].Animation.Should().Be("AmovPercMstpSnonWnonDnon");
        savedSession.Players["7656119800001"].Direction.Should().Be(180.0);
    }

    [Fact]
    public async Task HandleSaveAsync_WithUnexpectedDataType_LogsErrorAndDoesNotSave()
    {
        // sessionData should be a hashmap (Dict / pair-list). Anything else means the
        // wire contract drifted — must be loud, not a silent empty-save warning.
        await _subject.HandleSaveAsync("bad-key", string.Empty, 42L);

        _mockContext.Verify(x => x.Add(It.IsAny<DomainPersistenceSession>()), Times.Never);
        _mockContext.Verify(x => x.Replace(It.IsAny<DomainPersistenceSession>()), Times.Never);
        _mockLogger.Verify(x => x.LogError(It.Is<string>(s => s.Contains("unexpected type") && s.Contains("bad-key")), It.IsAny<Exception>()), Times.Once);
    }
}
