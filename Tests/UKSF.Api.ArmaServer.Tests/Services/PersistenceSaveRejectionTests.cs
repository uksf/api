using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.Converters;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models.Persistence;
using UKSF.Api.ArmaServer.Parsing;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class PersistenceSaveRejectionTests
{
    private readonly Mock<IPersistenceSessionsContext> _mockContext = new();
    private readonly PersistenceSessionsService _subject;

    public PersistenceSaveRejectionTests()
    {
        _subject = new PersistenceSessionsService(_mockContext.Object, new Mock<IUksfLogger>().Object);
    }

    private static Dictionary<string, object> SqfToDict(string sqf) => PersistenceConversionHelpers.ToDict(SqfNotationParser.ParseAndNormalize(sqf));

    [Theory]
    [InlineData("[[\"objects\",42],[\"players\",[]]]", "*'objects' is not a list*")]
    [InlineData("[[\"objects\",[]],[\"players\",42]]", "*'players' is not a hashmap*")]
    [InlineData("[[\"objects\",[]],[\"dateTime\",[[2035]]]]", "*Failed to deserialize*")]
    [InlineData("[[\"objects\",[[[\"turretWeapons\",[[]]]]]]]", "*Failed to deserialize*")]
    public async Task HandleSaveAsync_WithMalformedFields_RejectsWithoutSaving(string sqf, string expectedMessage)
    {
        var act = () => _subject.HandleSaveAsync("bad-key", string.Empty, SqfToDict(sqf));

        await act.Should().ThrowAsync<PersistenceSaveRejectedException>().WithMessage(expectedMessage);
        _mockContext.Verify(x => x.SaveIfNewer(It.IsAny<DomainPersistenceSession>(), It.IsAny<string>()), Times.Never);
    }
}
