using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Moq;
using UKSF.Api.ArmaServer.Controllers;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.ArmaServer.Signalr.Clients;
using UKSF.Api.ArmaServer.Signalr.Hubs;
using UKSF.Api.Core;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Controllers;

public class MissionsControllerTests
{
    private readonly Mock<IMissionsService> _mockMissionsService = new();
    private readonly Mock<IHubContext<ServersHub, IServersClient>> _mockServersHub = new();
    private readonly Mock<IHubClients<IServersClient>> _mockClients = new();
    private readonly Mock<IServersClient> _mockClient = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly MissionsController _sut;

    public MissionsControllerTests()
    {
        _mockServersHub.Setup(x => x.Clients).Returns(_mockClients.Object);
        _mockClients.Setup(x => x.All).Returns(_mockClient.Object);
        _mockClients.Setup(x => x.AllExcept(It.IsAny<IReadOnlyList<string>>())).Returns(_mockClient.Object);
        _mockClient.Setup(x => x.ReceiveMissionsUpdate(It.IsAny<List<MissionFile>>())).Returns(Task.CompletedTask);

        _sut = new MissionsController(_mockMissionsService.Object, _mockServersHub.Object, _mockLogger.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    [Fact]
    public void GetActiveMissions_ReturnsActiveMissions()
    {
        var missions = new List<MissionFile>();
        _mockMissionsService.Setup(x => x.GetActiveMissions()).Returns(missions);

        var result = _sut.GetActiveMissions();

        result.Should().BeSameAs(missions);
        _mockMissionsService.Verify(x => x.GetActiveMissions(), Times.Once);
    }

    [Fact]
    public void GetArchivedMissions_ReturnsArchivedMissions()
    {
        var missions = new List<MissionFile>();
        _mockMissionsService.Setup(x => x.GetArchivedMissions()).Returns(missions);

        var result = _sut.GetArchivedMissions();

        result.Should().BeSameAs(missions);
        _mockMissionsService.Verify(x => x.GetArchivedMissions(), Times.Once);
    }

    [Fact]
    public async Task DeleteMissionFile_CallsServiceAndWritesAudit()
    {
        const string fileName = "co40_test.Altis.pbo";
        _mockMissionsService.Setup(x => x.GetActiveMissions()).Returns([]);

        var result = await _sut.DeleteMissionFile(fileName);

        result.Should().BeOfType<OkResult>();
        _mockMissionsService.Verify(x => x.DeleteMissionFile(fileName), Times.Once);
        _mockLogger.Verify(x => x.LogAudit(It.Is<string>(s => s.Contains("Deleted") && s.Contains(fileName)), It.IsAny<string>()), Times.Once);
        _mockClient.Verify(x => x.ReceiveMissionsUpdate(It.IsAny<List<MissionFile>>()), Times.Once);
    }

    [Fact]
    public async Task DeleteMissionFile_MissingFile_Returns404_NoAudit()
    {
        const string fileName = "missing.pbo";
        _mockMissionsService.Setup(x => x.DeleteMissionFile(fileName)).Throws(new FileNotFoundException());

        var result = await _sut.DeleteMissionFile(fileName);

        result.Should().BeOfType<NotFoundResult>();
        _mockLogger.Verify(x => x.LogAudit(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ArchiveMissionFile_CallsServiceAndWritesAudit()
    {
        const string fileName = "co40_test.Altis.pbo";
        _mockMissionsService.Setup(x => x.GetActiveMissions()).Returns([]);

        var result = await _sut.ArchiveMissionFile(fileName);

        result.Should().BeOfType<OkResult>();
        _mockMissionsService.Verify(x => x.ArchiveMissionFile(fileName), Times.Once);
        _mockLogger.Verify(x => x.LogAudit(It.Is<string>(s => s.Contains("Archived") && s.Contains(fileName)), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ArchiveMissionFile_MissingFile_Returns404_NoAudit()
    {
        const string fileName = "missing.pbo";
        _mockMissionsService.Setup(x => x.ArchiveMissionFile(fileName)).Throws(new FileNotFoundException());

        var result = await _sut.ArchiveMissionFile(fileName);

        result.Should().BeOfType<NotFoundResult>();
        _mockLogger.Verify(x => x.LogAudit(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RestoreMissionFile_CallsServiceAndWritesAudit()
    {
        const string fileName = "co40_test.Altis.pbo";
        _mockMissionsService.Setup(x => x.GetActiveMissions()).Returns([]);

        var result = await _sut.RestoreMissionFile(fileName);

        result.Should().BeOfType<OkResult>();
        _mockMissionsService.Verify(x => x.RestoreMissionFile(fileName), Times.Once);
        _mockLogger.Verify(x => x.LogAudit(It.Is<string>(s => s.Contains("Restored") && s.Contains(fileName)), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task RestoreMissionFile_MissingFile_Returns404_NoAudit()
    {
        const string fileName = "missing.pbo";
        _mockMissionsService.Setup(x => x.RestoreMissionFile(fileName)).Throws(new FileNotFoundException());

        var result = await _sut.RestoreMissionFile(fileName);

        result.Should().BeOfType<NotFoundResult>();
        _mockLogger.Verify(x => x.LogAudit(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SendMissionsUpdate_WithConnectionId_ExcludesCaller()
    {
        _sut.ControllerContext.HttpContext.Request.Headers["Hub-Connection-Id"] = "conn-123";
        _mockMissionsService.Setup(x => x.GetActiveMissions()).Returns([]);

        await _sut.DeleteMissionFile("co40_test.Altis.pbo");

        _mockClients.Verify(x => x.AllExcept(It.Is<IReadOnlyList<string>>(l => l.Contains("conn-123"))), Times.Once);
        _mockClients.Verify(x => x.All, Times.Never);
    }

    [Fact]
    public void Controller_RequiresNcoServersOrCommandPermission()
    {
        var attribute = typeof(MissionsController).GetCustomAttributes(typeof(PermissionsAttribute), inherit: false).Cast<PermissionsAttribute>().Single();

        var roles = attribute.Roles!.Split(',');

        roles.Should().BeEquivalentTo(Permissions.Nco, Permissions.Servers, Permissions.Command);
    }
}
