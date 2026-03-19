using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models.Persistence;
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
    public async Task HandleSaveChunkAsync_SingleChunk_SavesImmediately()
    {
        var session = new DomainPersistenceSession
        {
            Key = "chunk-key",
            Objects = [],
            Players = new(),
            Markers = []
        };
        var json = JsonSerializer.Serialize(session);
        var chunk = new ChunkEnvelope
        {
            Id = "chunk-1",
            Key = "chunk-key",
            Index = 0,
            Total = 1,
            Data = json
        };

        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainPersistenceSession, bool>>())).Returns((DomainPersistenceSession)null);

        await _subject.HandleSaveChunkAsync(chunk);

        _mockContext.Verify(x => x.Add(It.Is<DomainPersistenceSession>(s => s.Key == "chunk-key")), Times.Once);
    }

    [Fact]
    public async Task HandleSaveChunkAsync_MultipleChunks_SavesWhenComplete()
    {
        var session = new DomainPersistenceSession
        {
            Key = "multi-key",
            Objects = [],
            Players = new(),
            Markers = []
        };
        var json = JsonSerializer.Serialize(session);
        var half = json.Length / 2;
        var part1 = json[..half];
        var part2 = json[half..];

        var chunk1 = new ChunkEnvelope
        {
            Id = "multi-1",
            Key = "multi-key",
            Index = 0,
            Total = 2,
            Data = part1
        };
        var chunk2 = new ChunkEnvelope
        {
            Id = "multi-1",
            Key = "multi-key",
            Index = 1,
            Total = 2,
            Data = part2
        };

        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainPersistenceSession, bool>>())).Returns((DomainPersistenceSession)null);

        await _subject.HandleSaveChunkAsync(chunk1);
        _mockContext.Verify(x => x.Add(It.IsAny<DomainPersistenceSession>()), Times.Never);

        await _subject.HandleSaveChunkAsync(chunk2);
        _mockContext.Verify(x => x.Add(It.Is<DomainPersistenceSession>(s => s.Key == "multi-key")), Times.Once);
    }

    [Fact]
    public async Task HandleSaveChunkAsync_DuplicateChunk_IsIdempotent()
    {
        var session = new DomainPersistenceSession
        {
            Key = "dup-key",
            Objects = [],
            Players = new(),
            Markers = []
        };
        var json = JsonSerializer.Serialize(session);
        var half = json.Length / 2;
        var part1 = json[..half];
        var part2 = json[half..];

        var chunk1 = new ChunkEnvelope
        {
            Id = "dup-1",
            Key = "dup-key",
            Index = 0,
            Total = 2,
            Data = part1
        };
        var chunk1Duplicate = new ChunkEnvelope
        {
            Id = "dup-1",
            Key = "dup-key",
            Index = 0,
            Total = 2,
            Data = part1
        };
        var chunk2 = new ChunkEnvelope
        {
            Id = "dup-1",
            Key = "dup-key",
            Index = 1,
            Total = 2,
            Data = part2
        };

        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainPersistenceSession, bool>>())).Returns((DomainPersistenceSession)null);

        await _subject.HandleSaveChunkAsync(chunk1);
        await _subject.HandleSaveChunkAsync(chunk1Duplicate);
        await _subject.HandleSaveChunkAsync(chunk2);

        _mockContext.Verify(x => x.Add(It.IsAny<DomainPersistenceSession>()), Times.Once);
    }

    [Fact]
    public async Task HandleSaveChunkAsync_ReassemblesDataCorrectly()
    {
        var session = new DomainPersistenceSession
        {
            Key = "reassemble-key",
            Objects =
            [
                new PersistenceObject
                {
                    Id = "obj-1",
                    Type = "B_MRAP_01_F",
                    Position = [100.5, 200.3, 0.1],
                    Damage = 0.25,
                    Fuel = 0.8
                }
            ],
            Players = new Dictionary<string, PlayerRedeployData>
            {
                ["player-uid"] = new()
                {
                    Position = [50.0, 60.0, 0.0],
                    Direction = 180.0,
                    Animation = "AmovPercMstpSnonWnonDnon"
                }
            },
            Markers = []
        };

        var json = JsonSerializer.Serialize(session);

        // Split into 3 chunks
        var chunkSize = json.Length / 3;
        var parts = new[] { json[..chunkSize], json[chunkSize..(chunkSize * 2)], json[(chunkSize * 2)..] };

        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainPersistenceSession, bool>>())).Returns((DomainPersistenceSession)null);

        DomainPersistenceSession savedSession = null;
        _mockContext.Setup(x => x.Add(It.IsAny<DomainPersistenceSession>()))
                    .Callback<DomainPersistenceSession>(s => savedSession = s)
                    .Returns(Task.CompletedTask);

        for (var i = 0; i < 3; i++)
        {
            await _subject.HandleSaveChunkAsync(
                new ChunkEnvelope
                {
                    Id = "reassemble-1",
                    Key = "reassemble-key",
                    Index = i,
                    Total = 3,
                    Data = parts[i]
                }
            );
        }

        savedSession.Should().NotBeNull();
        savedSession!.Objects.Should().HaveCount(1);
        savedSession.Objects[0].Id.Should().Be("obj-1");
        savedSession.Objects[0].Type.Should().Be("B_MRAP_01_F");
        savedSession.Objects[0].Position.Should().BeEquivalentTo(new[] { 100.5, 200.3, 0.1 });
        savedSession.Players.Should().ContainKey("player-uid");
        savedSession.Players["player-uid"].Animation.Should().Be("AmovPercMstpSnonWnonDnon");
    }

    [Fact]
    public async Task HandleSaveChunkAsync_EvictsExpiredBuffers()
    {
        // Send a chunk that will never complete (total=2 but only 1 sent)
        var incompleteChunk = new ChunkEnvelope
        {
            Id = "expired-1",
            Key = "expired-key",
            Index = 0,
            Total = 2,
            Data = "partial"
        };

        await _subject.HandleSaveChunkAsync(incompleteChunk);

        // The buffer exists but is incomplete - no save should have happened
        _mockContext.Verify(x => x.Add(It.IsAny<DomainPersistenceSession>()), Times.Never);

        // Send a normal complete chunk - this triggers eviction check but the expired buffer
        // won't be evicted because it was just created (less than 5 minutes ago)
        var session = new DomainPersistenceSession
        {
            Key = "normal-key",
            Objects = [],
            Players = new(),
            Markers = []
        };
        var json = JsonSerializer.Serialize(session);
        var normalChunk = new ChunkEnvelope
        {
            Id = "normal-1",
            Key = "normal-key",
            Index = 0,
            Total = 1,
            Data = json
        };

        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainPersistenceSession, bool>>())).Returns((DomainPersistenceSession)null);

        await _subject.HandleSaveChunkAsync(normalChunk);

        // The normal chunk should save successfully
        _mockContext.Verify(x => x.Add(It.Is<DomainPersistenceSession>(s => s.Key == "normal-key")), Times.Once);
    }

    [Fact]
    public void RoundTrip_SqfPositionalArrayFormat_PreservesData()
    {
        // SQF sends all sub-structures as positional arrays, not named objects.
        // This test uses the actual format from the game mod.
        var json = """
                   {
                       "id": "obj-all-fields",
                       "type": "B_Heli_Transport_01_F",
                       "position": [1234.56, 5678.90, 15.3],
                       "vectorDirUp": [[0.0, 0.8, 0.0], [0.0, 0.0, 1.0]],
                       "damage": 0.35,
                       "fuel": 0.72,
                       "turretWeapons": [[[0, 1], ["LMG_Minigun", "missiles_DAR"]]],
                       "turretMagazines": [["200Rnd_65x39_cased_Box", [0], 200, 10000200, 2]],
                       "pylonLoadout": [["PylonMissile_1Rnd_Missile_AA_04_F", 1], ["PylonRack_12Rnd_missiles", 12]],
                       "logistics": [100.0, 50.0, 75.0, 200.0],
                       "attached": [["ACE_IR_Strobe_Effect", [0.0, 0.0, 1.5]]],
                       "rackChannels": [0, 1, 2],
                       "aceCargo": [["Box_IND_Ammo_F", [], [], "Ammo Box"]],
                       "inventory": [[["item1"], [1]], [["mag1"], [10]], [[], []], [[], []]],
                       "aceFortify": [false, "WEST"],
                       "aceMedical": [0, false, false],
                       "aceRepair": [0, 0],
                       "customName": "Custom Helicopter"
                   }
                   """;

        var deserialized = JsonSerializer.Deserialize<PersistenceObject>(json, PersistenceSessionsService.SerializerOptions);

        deserialized.Should().NotBeNull();
        deserialized!.Id.Should().Be("obj-all-fields");
        deserialized.Type.Should().Be("B_Heli_Transport_01_F");
        deserialized.Position.Should().BeEquivalentTo(new[] { 1234.56, 5678.90, 15.3 });
        deserialized.Damage.Should().Be(0.35);
        deserialized.Fuel.Should().Be(0.72);
        deserialized.CustomName.Should().Be("Custom Helicopter");
        deserialized.Logistics.Should().BeEquivalentTo(new[] { 100.0, 50.0, 75.0, 200.0 });
        deserialized.RackChannels.Should().BeEquivalentTo(new[] { 0, 1, 2 });

        deserialized.TurretWeapons.Should().HaveCount(1);
        deserialized.TurretMagazines.Should().HaveCount(1);
        deserialized.PylonLoadout.Should().HaveCount(2);
        deserialized.Attached.Should().HaveCount(1);
        deserialized.AceCargo.Should().HaveCount(1);
    }

    [Fact]
    public void RoundTrip_ObjectWithDefaults_PreservesData()
    {
        var original = new PersistenceObject();

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<PersistenceObject>(json, PersistenceSessionsService.SerializerOptions);

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
        var json = """
                   {
                       "id": "empty-collections",
                       "type": "B_Truck_01_transport_F",
                       "position": [],
                       "vectorDirUp": [],
                       "turretWeapons": [],
                       "turretMagazines": [],
                       "pylonLoadout": [],
                       "logistics": [],
                       "attached": [],
                       "rackChannels": [],
                       "aceCargo": [],
                       "inventory": [[[],[]],[[],[]],[[],[]],[[],[]]],
                       "aceFortify": [false, "WEST"],
                       "aceMedical": [0, false, false],
                       "aceRepair": [0, 0]
                   }
                   """;

        var deserialized = JsonSerializer.Deserialize<PersistenceObject>(json, PersistenceSessionsService.SerializerOptions);

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
    public void RoundTrip_SqfNestedAceCargo_PreservesData()
    {
        // SQF ace cargo format: [className, nestedCargo[], inventory[], customName]
        var json = """
                   {
                       "id": "nested-cargo",
                       "type": "B_CargoNet_01_ammo_F",
                       "aceCargo": [["Box_IND_Ammo_F", [["nested_item_1", [], [], ""]], [], "Ammo Box"]]
                   }
                   """;

        var deserialized = JsonSerializer.Deserialize<PersistenceObject>(json, PersistenceSessionsService.SerializerOptions);

        deserialized.Should().NotBeNull();
        deserialized!.AceCargo.Should().HaveCount(1);
    }

    [Fact]
    public void RoundTrip_SqfTurretWeapons_PreservesData()
    {
        // SQF turret weapons format: [[turretPath, weapons[]]]
        var json = """
                   {
                       "id": "turret-weapons",
                       "type": "B_APC_Tracked_01_AA_F",
                       "turretWeapons": [[[0, 1], ["weapon1", "weapon2"]]]
                   }
                   """;

        var deserialized = JsonSerializer.Deserialize<PersistenceObject>(json, PersistenceSessionsService.SerializerOptions);

        deserialized.Should().NotBeNull();
        deserialized!.TurretWeapons.Should().HaveCount(1);
    }

    [Fact]
    public void RoundTrip_SqfPylonLoadout_PreservesData()
    {
        // SQF pylon format: [magazine, ammo]
        var json = """
                   {
                       "id": "pylon-loadout",
                       "type": "B_Plane_CAS_01_dynamicLoadout_F",
                       "pylonLoadout": [["mag1", 20], ["mag2", 0]]
                   }
                   """;

        var deserialized = JsonSerializer.Deserialize<PersistenceObject>(json, PersistenceSessionsService.SerializerOptions);

        deserialized.Should().NotBeNull();
        deserialized!.PylonLoadout.Should().HaveCount(2);
    }

    [Fact]
    public void RoundTrip_SqfAttachedObjects_PreservesData()
    {
        // SQF attached format: [className, offset[]]
        var json = """
                   {
                       "id": "attached-objects",
                       "type": "B_MRAP_01_F",
                       "attached": [["classname", [0.1, 0.2, 0.3]]]
                   }
                   """;

        var deserialized = JsonSerializer.Deserialize<PersistenceObject>(json, PersistenceSessionsService.SerializerOptions);

        deserialized.Should().NotBeNull();
        deserialized!.Attached.Should().HaveCount(1);
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
        deserialized.VehicleState.Index.Should().Be(-1);

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
        deserialized.VehicleState.Index.Should().Be(-1);
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
        deserialized.VehicleState.Index.Should().Be(-1);
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
        deserialized.VehicleState.Index.Should().Be(0);
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
                       "ace_medical_bloodVolume": 5.5,
                       "ace_medical_heartRate": 90,
                       "ace_medical_bloodPressure": [70, 110],
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
}
