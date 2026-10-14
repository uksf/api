using System.Collections.Generic;
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

public class PersistenceSaveValidationTests
{
    private readonly Mock<IPersistenceSessionsContext> _mockContext = new();
    private readonly PersistenceSessionsService _subject;

    public PersistenceSaveValidationTests()
    {
        _subject = new PersistenceSessionsService(_mockContext.Object, new Mock<IUksfLogger>().Object);
    }

    private static object Sqf(string sqf) => SqfNotationParser.ParseAndNormalize(sqf);

    [Theory]
    [InlineData("""[["objects",[42]],["players",[]]]""", "*'objects' holds an entry*")]
    [InlineData("""[["objects",[[]]],["players",[]]]""", "*'objects' holds an entry*")]
    [InlineData("""[["unexpected",1]]""", "*no 'objects' field*")]
    [InlineData("""[["objects",[]],["players",[["765",7]]]]""", "*'players' holds an entry*")]
    [InlineData("""[["objects",[]],["players",[42]]]""", "*'players' holds an entry*")]
    [InlineData("""[["objects",[]],["players",[["765",[["name","a"]]],42]]]""", "*'players' holds an entry*")]
    [InlineData("""[["objects",[[["type","Box"],42]]],["players",[]]]""", "*'objects' holds an entry*")]
    public async Task HandleSaveAsync_WithMalformedNestedData_RejectsWithoutSaving(string sqf, string message)
    {
        var act = () => _subject.HandleSaveAsync("bad-key", "session", Sqf(sqf));

        await act.Should().ThrowAsync<PersistenceSaveRejectedException>().WithMessage(message);
        _mockContext.Verify(x => x.SaveIfNewer(It.IsAny<DomainPersistenceSession>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData("""[["junk",true]]""")]
    [InlineData("""[["type","Box"],["position",[1,2,3]],["vectorDirUp",[[0,1,0],[0,0,1]]]]""")]
    [InlineData("""[["id",""],["type","Box"],["position",[1,2,3]],["vectorDirUp",[[0,1,0],[0,0,1]]]]""")]
    [InlineData("""[["id","box_1"],["position",[1,2,3]],["vectorDirUp",[[0,1,0],[0,0,1]]]]""")]
    [InlineData("""[["id","box_1"],["type","Box"],["position",[1,2]],["vectorDirUp",[[0,1,0],[0,0,1]]]]""")]
    [InlineData("""[["id","box_1"],["type","Box"],["position",[1,2,3]]]""")]
    [InlineData("""[["id","box_1"],["type","Box"],["position",[1,2,3]],["vectorDirUp",[[0,1,0],[0,0]]]]""")]
    public async Task HandleSaveAsync_WithAnObjectMissingItsIdentityOrPlacement_RejectsWithoutSaving(string objectSqf)
    {
        var act = () => _subject.HandleSaveAsync("bad-key", "session", Sqf($"""[["objects",[{objectSqf}]],["players",[]]]"""));

        await act.Should().ThrowAsync<PersistenceSaveRejectedException>().WithMessage("*'objects'*");
        _mockContext.Verify(x => x.SaveIfNewer(It.IsAny<DomainPersistenceSession>(), It.IsAny<string>()), Times.Never);
    }

    private const string ValidPlayer =
        """[["position",[1,2,3]],["vehicleState",["","",-1]],["direction",90],["animation",""],["loadout",[[],[],[],[],[],[],"","",[],[]]],["damage",0.2],["aceMedical",[]],["earplugs",false],["attachedItems",[]],["radios",[]],["diveState",[false]]]""";

    [Theory]
    [InlineData("position", "[1,2]")]
    [InlineData("vehicleState", "[\"\",\"cargo\",0]")]
    [InlineData("vehicleState", "[\"veh_1\",\"\",0]")]
    [InlineData("direction", "\"north\"")]
    [InlineData("damage", "2")]
    [InlineData("loadout", "[[],[]]")]
    [InlineData("aceMedical", "\"{not json\"")]
    [InlineData("earplugs", "1")]
    [InlineData("attachedItems", "[1]")]
    [InlineData("diveState", "[]")]
    public async Task HandleSaveAsync_WithAMalformedPlayerField_RejectsWithoutSaving(string field, string value)
    {
        var player = ValidPlayer.Replace($"[\"{field}\",", $"[\"{field}\",{value}],[\"_{field}\",");

        var act = () => _subject.HandleSaveAsync("bad-key", "session", Sqf($"""[["objects",[]],["players",[["765",{player}]]]]"""));

        await act.Should().ThrowAsync<PersistenceSaveRejectedException>().WithMessage($"*{field}*");
        _mockContext.Verify(x => x.SaveIfNewer(It.IsAny<DomainPersistenceSession>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData("""["","",-1]""")]
    [InlineData("""["veh_1","cargo",2]""")]
    [InlineData("""["veh_1","turret",[0,1]]""")]
    public async Task HandleSaveAsync_WithAValidPlayer_Saves(string vehicleState)
    {
        _mockContext.Setup(x => x.SaveIfNewer(It.IsAny<DomainPersistenceSession>(), "session")).ReturnsAsync(true);
        var player = ValidPlayer.Replace("""["vehicleState",["","",-1]]""", $"""["vehicleState",{vehicleState}]""");

        await _subject.HandleSaveAsync("key", "session", Sqf($"""[["objects",[]],["players",[["765",{player}]]]]"""));

        _mockContext.Verify(x => x.SaveIfNewer(It.Is<DomainPersistenceSession>(s => s.Players.ContainsKey("765")), "session"), Times.Once);
    }

    [Fact]
    public async Task HandleSaveAsync_WithTwoObjectsSharingAnId_RejectsWithoutSaving()
    {
        const string box = """[["id","box_1"],["type","Box"],["position",[1,2,3]],["vectorDirUp",[[0,1,0],[0,0,1]]]]""";

        var act = () => _subject.HandleSaveAsync("bad-key", "session", Sqf($"""[["objects",[{box},{box}]],["players",[]]]"""));

        await act.Should().ThrowAsync<PersistenceSaveRejectedException>().WithMessage("*box_1*more than once*");
    }

    [Fact]
    public async Task HandleSaveAsync_WithAPlacedObject_Saves()
    {
        _mockContext.Setup(x => x.SaveIfNewer(It.IsAny<DomainPersistenceSession>(), "session")).ReturnsAsync(true);

        await _subject.HandleSaveAsync(
            "key",
            "session",
            Sqf("""[["objects",[[["id","box_1"],["type","Box"],["position",[1,2,3]],["vectorDirUp",[[0,1,0],[0,0,1]]]]]],["players",[]]]""")
        );

        _mockContext.Verify(
            x => x.SaveIfNewer(It.Is<DomainPersistenceSession>(s => s.Objects.Count == 1 && s.Objects[0].Id == "box_1"), "session"),
            Times.Once
        );
    }

    [Fact]
    public async Task HandleSaveAsync_WithNoObjectsAndNoPlayers_Saves()
    {
        DomainPersistenceSession saved = null;
        _mockContext.Setup(x => x.SaveIfNewer(It.IsAny<DomainPersistenceSession>(), "session"))
                    .Callback<DomainPersistenceSession, string>((session, _) => saved = session)
                    .ReturnsAsync(true);

        await _subject.HandleSaveAsync("empty-key", "session", Sqf("""[["objects",[]],["players",[]]]"""));

        saved.Should().NotBeNull();
        saved.Key.Should().Be("empty-key");
        saved.Objects.Should().BeEmpty();
    }
}
