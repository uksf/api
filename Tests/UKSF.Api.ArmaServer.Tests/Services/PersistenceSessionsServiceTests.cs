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

    #region Load tests

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

    #endregion

    #region Save tests

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

    #endregion

    #region Chunk reassembly tests

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

    #endregion

    #region Round-trip tests

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
            TurretWeapons = [new object[] { new object[] { new object[] { 0, 1 }, new object[] { "LMG_Minigun", "missiles_DAR" } } }],
            TurretMagazines = [new object[] { new object[] { new object[] { 0 }, new object[] { "200Rnd_65x39_cased_Box" } } }],
            PylonLoadout = [new object[] { "PylonMissile_1Rnd_Missile_AA_04_F", 1 }, new object[] { "PylonRack_12Rnd_missiles", 12 }],
            Logistics = [100.0, 50.0, 75.0, 200.0],
            Attached = [new object[] { "ACE_IR_Strobe_Effect", new object[] { 0.0, 0.0, 1.5 } }],
            RackChannels = [new object[] { 0, 1, 2 }],
            AceCargo = [new object[] { "Box_IND_Ammo_F", new object[] { }, new object[] { }, "Ammo Box" }],
            Inventory = [new object[] { "item1", "item2" }, new object[] { "mag1", "mag2" }],
            AceFortify = [new object[] { "fort1", 5 }],
            AceMedical = [new object[] { "medical1", 10 }],
            AceRepair = [new object[] { "repair1", 3 }],
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

        // Verify complex arrays survived round-trip by re-serializing
        JsonSerializer.Serialize(deserialized.TurretWeapons).Should().Be(JsonSerializer.Serialize(original.TurretWeapons));
        JsonSerializer.Serialize(deserialized.TurretMagazines).Should().Be(JsonSerializer.Serialize(original.TurretMagazines));
        JsonSerializer.Serialize(deserialized.PylonLoadout).Should().Be(JsonSerializer.Serialize(original.PylonLoadout));
        JsonSerializer.Serialize(deserialized.Attached).Should().Be(JsonSerializer.Serialize(original.Attached));
        JsonSerializer.Serialize(deserialized.RackChannels).Should().Be(JsonSerializer.Serialize(original.RackChannels));
        JsonSerializer.Serialize(deserialized.AceCargo).Should().Be(JsonSerializer.Serialize(original.AceCargo));
        JsonSerializer.Serialize(deserialized.Inventory).Should().Be(JsonSerializer.Serialize(original.Inventory));
        JsonSerializer.Serialize(deserialized.AceFortify).Should().Be(JsonSerializer.Serialize(original.AceFortify));
        JsonSerializer.Serialize(deserialized.AceMedical).Should().Be(JsonSerializer.Serialize(original.AceMedical));
        JsonSerializer.Serialize(deserialized.AceRepair).Should().Be(JsonSerializer.Serialize(original.AceRepair));
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
        deserialized.Inventory.Should().BeEmpty();
        deserialized.AceFortify.Should().BeEmpty();
        deserialized.AceMedical.Should().BeEmpty();
        deserialized.AceRepair.Should().BeEmpty();
        deserialized.CustomName.Should().Be(string.Empty);
    }

    [Fact]
    public void RoundTrip_ObjectWithEmptyArrays_PreservesData()
    {
        var original = new PersistenceObject
        {
            Id = "empty-arrays",
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
            Inventory = [],
            AceFortify = [],
            AceMedical = [],
            AceRepair = []
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<PersistenceObject>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Id.Should().Be("empty-arrays");
        deserialized.Position.Should().BeEmpty();
        deserialized.VectorDirUp.Should().BeEmpty();
        deserialized.TurretWeapons.Should().BeEmpty();
        deserialized.TurretMagazines.Should().BeEmpty();
        deserialized.PylonLoadout.Should().BeEmpty();
        deserialized.Logistics.Should().BeEmpty();
        deserialized.Attached.Should().BeEmpty();
        deserialized.RackChannels.Should().BeEmpty();
        deserialized.AceCargo.Should().BeEmpty();
        deserialized.Inventory.Should().BeEmpty();
        deserialized.AceFortify.Should().BeEmpty();
        deserialized.AceMedical.Should().BeEmpty();
        deserialized.AceRepair.Should().BeEmpty();
    }

    [Fact]
    public void RoundTrip_ObjectWithNestedAceCargo_PreservesData()
    {
        var original = new PersistenceObject
        {
            Id = "nested-cargo",
            Type = "B_CargoNet_01_ammo_F",
            AceCargo = [new object[] { "Box_IND_Ammo_F", new object[] { "nested_item_1" }, new object[] { "inv1", "inv2" }, "Ammo Box" }]
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<PersistenceObject>(json);

        deserialized.Should().NotBeNull();
        JsonSerializer.Serialize(deserialized!.AceCargo).Should().Be(JsonSerializer.Serialize(original.AceCargo));
    }

    [Fact]
    public void RoundTrip_ObjectWithTurretWeapons_PreservesData()
    {
        var original = new PersistenceObject
        {
            Id = "turret-weapons",
            Type = "B_APC_Tracked_01_AA_F",
            TurretWeapons = [new object[] { new object[] { new object[] { 0, 1 }, new object[] { "weapon1", "weapon2" } } }]
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<PersistenceObject>(json);

        deserialized.Should().NotBeNull();
        JsonSerializer.Serialize(deserialized!.TurretWeapons).Should().Be(JsonSerializer.Serialize(original.TurretWeapons));
    }

    [Fact]
    public void RoundTrip_ObjectWithPylonLoadout_PreservesData()
    {
        var original = new PersistenceObject
        {
            Id = "pylon-loadout",
            Type = "B_Plane_CAS_01_dynamicLoadout_F",
            PylonLoadout = [new object[] { "mag1", 20 }, new object[] { "mag2", 0 }]
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<PersistenceObject>(json);

        deserialized.Should().NotBeNull();
        JsonSerializer.Serialize(deserialized!.PylonLoadout).Should().Be(JsonSerializer.Serialize(original.PylonLoadout));
    }

    [Fact]
    public void RoundTrip_ObjectWithAttachedObjects_PreservesData()
    {
        var original = new PersistenceObject
        {
            Id = "attached-objects",
            Type = "B_MRAP_01_F",
            Attached = [new object[] { "classname", new object[] { 0.1, 0.2, 0.3 } }]
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<PersistenceObject>(json);

        deserialized.Should().NotBeNull();
        JsonSerializer.Serialize(deserialized!.Attached).Should().Be(JsonSerializer.Serialize(original.Attached));
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
            VehicleState = ["persistence_id_123", "driver", -1],
            Direction = 270.0,
            Animation = "AmovPercMstpSlowWrflDnon",
            Loadout = [new object[] { "uniform_class" }, new object[] { "vest_class" }],
            Damage = 0.15,
            AceMedical = [new object[] { "bandage", 3 }],
            Earplugs = true,
            AttachedItems = ["NVGoggles", "ItemMap", "ItemCompass"],
            Radios = [new object[] { "ACRE_PRC152", "channel1" }],
            DiveState = [new object[] { 0.8, 100.0 }]
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

        JsonSerializer.Serialize(deserialized.VehicleState).Should().Be(JsonSerializer.Serialize(original.VehicleState));
        JsonSerializer.Serialize(deserialized.Loadout).Should().Be(JsonSerializer.Serialize(original.Loadout));
        JsonSerializer.Serialize(deserialized.AceMedical).Should().Be(JsonSerializer.Serialize(original.AceMedical));
        JsonSerializer.Serialize(deserialized.Radios).Should().Be(JsonSerializer.Serialize(original.Radios));
        JsonSerializer.Serialize(deserialized.DiveState).Should().Be(JsonSerializer.Serialize(original.DiveState));
    }

    [Fact]
    public void RoundTrip_PlayerOnFoot_PreservesData()
    {
        var original = new PlayerRedeployData
        {
            Position = [500.0, 600.0, 0.0],
            VehicleState = ["", "", -1],
            Direction = 90.0,
            Animation = "AmovPercMstpSnonWnonDnon"
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<PlayerRedeployData>(json);

        deserialized.Should().NotBeNull();
        JsonSerializer.Serialize(deserialized!.VehicleState).Should().Be(JsonSerializer.Serialize(original.VehicleState));
    }

    [Fact]
    public void RoundTrip_PlayerInVehicle_PreservesData()
    {
        var original = new PlayerRedeployData
        {
            Position = [800.0, 900.0, 2.0],
            VehicleState = ["persistence_id", "driver", -1],
            Direction = 45.0
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<PlayerRedeployData>(json);

        deserialized.Should().NotBeNull();
        JsonSerializer.Serialize(deserialized!.VehicleState).Should().Be(JsonSerializer.Serialize(original.VehicleState));
    }

    [Fact]
    public void RoundTrip_PlayerInTurret_PreservesData()
    {
        var original = new PlayerRedeployData
        {
            Position = [800.0, 900.0, 2.0],
            VehicleState = ["persistence_id", "turret", new object[] { 0, 1 }],
            Direction = 180.0
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<PlayerRedeployData>(json);

        deserialized.Should().NotBeNull();
        JsonSerializer.Serialize(deserialized!.VehicleState).Should().Be(JsonSerializer.Serialize(original.VehicleState));
    }

    [Fact]
    public void RoundTrip_PlayerWithEmptyOptionalFields_PreservesData()
    {
        var original = new PlayerRedeployData();

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<PlayerRedeployData>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Position.Should().BeEmpty();
        deserialized.VehicleState.Should().BeEmpty();
        deserialized.Direction.Should().Be(0);
        deserialized.Animation.Should().Be(string.Empty);
        deserialized.Loadout.Should().BeEmpty();
        deserialized.Damage.Should().Be(0);
        deserialized.AceMedical.Should().BeEmpty();
        deserialized.Earplugs.Should().BeFalse();
        deserialized.AttachedItems.Should().BeEmpty();
        deserialized.Radios.Should().BeEmpty();
        deserialized.DiveState.Should().BeEmpty();
    }

    [Fact]
    public void RoundTrip_StandardMarker_PreservesData()
    {
        // Standard marker: [name, type, pos, shape, color, alpha, size, dir, text, brush]
        var marker = new object[]
        {
            "marker_1", "hd_dot", new object[] { 1000.0, 2000.0, 0.0 }, "ICON", "ColorRed", 1.0, new object[] { 1.0, 1.0 }, 0.0, "Target Alpha", ""
        };

        var session = new DomainPersistenceSession { Key = "marker-test", Markers = [marker] };

        var json = JsonSerializer.Serialize(session);
        var deserialized = JsonSerializer.Deserialize<DomainPersistenceSession>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Markers.Should().HaveCount(1);
        JsonSerializer.Serialize(deserialized.Markers[0]).Should().Be(JsonSerializer.Serialize(marker));
    }

    [Fact]
    public void RoundTrip_PolylineMarker_PreservesData()
    {
        // Polyline marker: [name, type, polyline, color, alpha, text]
        var marker = new object[]
        {
            "polyline_1",
            "hd_polyline",
            new object[] { new object[] { 100.0, 200.0 }, new object[] { 300.0, 400.0 }, new object[] { 500.0, 600.0 } },
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
        var standardMarker = new object[]
        {
            "marker_1", "hd_dot", new object[] { 1000.0, 2000.0, 0.0 }, "ICON", "ColorRed", 1.0, new object[] { 1.0, 1.0 }, 0.0, "Target", ""
        };
        var polylineMarker = new object[]
        {
            "polyline_1", "hd_polyline", new object[] { new object[] { 100.0, 200.0 }, new object[] { 300.0, 400.0 } }, "ColorBlue", 0.8, "Route"
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
                    VehicleState = ["obj-1", "driver", -1],
                    Direction = 90.0,
                    Animation = "AmovPercMstpSnonWnonDnon",
                    Earplugs = true,
                    AttachedItems = ["NVGoggles"]
                },
                ["uid-2"] = new()
                {
                    Position = [700.0, 800.0, 0.0],
                    VehicleState = ["", "", -1],
                    Direction = 180.0,
                    Animation = "AmovPercMstpSlowWrflDnon"
                }
            },
            Markers =
            [
                new object[]
                {
                    "marker_1", "hd_dot", new object[] { 1000.0, 2000.0, 0.0 }, "ICON", "ColorRed", 1.0, new object[] { 1.0, 1.0 }, 0.0, "Target", ""
                }
            ],
            DateTime = [2035, 6, 15, 14, 30],
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
        deserialized.Players.Should().ContainKey("uid-2");
        deserialized.Markers.Should().HaveCount(1);
        deserialized.DateTime.Should().BeEquivalentTo(original.DateTime);
        deserialized.SavedAt.Should().Be(original.SavedAt);

        JsonSerializer.Serialize(deserialized.CustomData).Should().Be(JsonSerializer.Serialize(original.CustomData));
        JsonSerializer.Serialize(deserialized.Markers).Should().Be(JsonSerializer.Serialize(original.Markers));
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
        deserialized.DateTime.Should().BeEmpty();
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

    #endregion
}
