using System.Collections.Generic;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using UKSF.Api.ArmaServer.Controllers;
using UKSF.Api.ArmaServer.Models.Persistence;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Controllers;

public class PersistenceControllerTests
{
    private readonly Mock<IPersistenceSessionsService> _mockService = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly PersistenceController _sut;

    public PersistenceControllerTests()
    {
        _sut = new PersistenceController(_mockService.Object, _mockLogger.Object);
        _sut.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
    }

    [Fact]
    public void Get_WithExistingKey_ReturnsJsonResult()
    {
        var session = new DomainPersistenceSession { Key = "test-key", Objects = new List<PersistenceObject>() };
        _mockService.Setup(x => x.Load("test-key")).Returns(session);

        var result = _sut.Get("test-key");

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var returnedSession = jsonResult.Value.Should().BeOfType<DomainPersistenceSession>().Subject;
        returnedSession.Key.Should().Be("test-key");
    }

    [Fact]
    public void Get_WithMissingKey_ReturnsNotFound()
    {
        _mockService.Setup(x => x.Load("missing-key")).Returns((DomainPersistenceSession)null);

        var result = _sut.Get("missing-key");

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void Get_LogsDebugMessage()
    {
        _mockService.Setup(x => x.Load("test-key")).Returns((DomainPersistenceSession)null);

        _sut.Get("test-key");

        _mockLogger.Verify(x => x.LogDebug(It.Is<string>(s => s.Contains("test-key"))), Times.Once);
    }

    [Fact]
    public void Get_WithFormatRaw_ShouldReturnRawNamespaceFormat()
    {
        var session = new DomainPersistenceSession
        {
            Key = "test-key",
            Objects =
            [
                new PersistenceObject
                {
                    Id = "obj-1",
                    Type = "B_MRAP_01_F",
                    Position = [100.5, 200.3, 0.1],
                    VectorDirUp = [[0, 1, 0], [0, 0, 1]],
                    Damage = 0.25,
                    Fuel = 0.8
                }
            ],
            Players = new Dictionary<string, PlayerRedeployData>
            {
                ["76561198012345678"] = new()
                {
                    Position = [50.0, 60.0, 0.0],
                    Direction = 180.0,
                    Animation = "AmovPercMstpSnonWnonDnon"
                }
            },
            ArmaDateTime = [2035, 6, 15, 12, 30],
            DeletedObjects = ["del-1"],
            Markers = []
        };
        _mockService.Setup(x => x.Load("test-key")).Returns(session);

        var result = _sut.Get("test-key", "raw");

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var raw = jsonResult.Value.Should().BeOfType<Dictionary<string, object>>().Subject;
        raw.Should().ContainKey("uksf_persistence_objects");
        raw.Should().ContainKey("uksf_persistence_dateTime");
        raw.Should().ContainKey("uksf_persistence_deletedObjects");
        raw.Should().ContainKey("uksf_persistence_mapMarkers");
        raw.Should().ContainKey("76561198012345678");
        raw.Should().NotContainKey("objects");
        raw.Should().NotContainKey("players");
    }
}
