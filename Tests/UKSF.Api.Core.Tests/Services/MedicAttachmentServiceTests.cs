using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.Core.Tests.Services;

public class MedicAttachmentServiceTests
{
    private readonly Mock<IAccountContext> _mockAccountContext = new();
    private readonly Mock<IUnitsContext> _mockUnitsContext = new();
    private readonly Mock<IChainOfCommandService> _mockChainOfCommandService = new();
    private readonly Mock<IDisplayNameService> _mockDisplayNameService = new();
    private readonly Mock<INotificationsService> _mockNotificationsService = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly MedicAttachmentService _subject;

    public MedicAttachmentServiceTests()
    {
        _subject = new MedicAttachmentService(
            _mockAccountContext.Object,
            _mockUnitsContext.Object,
            _mockChainOfCommandService.Object,
            _mockDisplayNameService.Object,
            _mockNotificationsService.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task SeverAttachment_WhenLinked_NullsFieldAuditsNotifies()
    {
        var account = new DomainAccount { Id = "rec", UnitAssignment = "SFM", AttachedTroop = "troop" };
        var troopUnit = new DomainUnit { Id = "troop", Name = "TroopUnit", ChainOfCommand = new ChainOfCommand { First = "troopCmdr" } };
        var sfmUnit = new DomainUnit { Id = "sfm", Name = "SFM", ChainOfCommand = new ChainOfCommand { First = "sfmCmdr" } };

        _mockAccountContext.Setup(x => x.GetSingle("rec")).Returns(account);
        _mockAccountContext.Setup(x => x.Update(It.IsAny<string>(), It.IsAny<Expression<Func<DomainAccount, string>>>(), It.IsAny<string>()))
                           .Returns(Task.CompletedTask);
        _mockUnitsContext.Setup(x => x.GetSingle("troop")).Returns(troopUnit);
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns(sfmUnit);
        _mockDisplayNameService.Setup(x => x.GetDisplayName(account)).Returns("Pvt Doe");

        var result = await _subject.SeverAttachment("rec", "test-trigger");

        result.Should().BeTrue();
        _mockAccountContext.Verify(x => x.Update("rec", It.IsAny<Expression<Func<DomainAccount, string>>>(), null), Times.Once);
        _mockNotificationsService.Verify(x => x.Add(It.Is<DomainNotification>(n => n.Owner == "rec")), Times.Once);
    }

    [Fact]
    public async Task SeverAttachment_WhenNotLinked_IsNoOp()
    {
        var account = new DomainAccount { Id = "rec", UnitAssignment = "SFM", AttachedTroop = null };
        _mockAccountContext.Setup(x => x.GetSingle("rec")).Returns(account);

        var result = await _subject.SeverAttachment("rec", "test-trigger");

        result.Should().BeFalse();
        _mockAccountContext.Verify(x => x.Update(It.IsAny<string>(), It.IsAny<Expression<Func<DomainAccount, string>>>(), It.IsAny<string>()), Times.Never);
    }
}
