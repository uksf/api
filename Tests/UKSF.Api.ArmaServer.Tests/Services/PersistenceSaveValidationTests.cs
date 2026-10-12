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
