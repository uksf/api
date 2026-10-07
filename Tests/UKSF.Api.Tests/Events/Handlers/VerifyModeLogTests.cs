using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.EventHandlers;
using Xunit;

namespace UKSF.Api.Tests.Events.Handlers;

public class VerifyModeLogTests
{
    private readonly Mock<IAuditLogContext> _auditLogs = new();
    private readonly Mock<IErrorLogContext> _errorLogs = new();
    private readonly Mock<ILogContext> _logs = new();
    private readonly Mock<ILauncherLogContext> _launcherLogs = new();
    private readonly Mock<IDiscordLogContext> _discordLogs = new();
    private readonly StringWriter _output = new();

    private UksfLoggerEventHandler CreateSubject(string verifyFlag)
    {
        Mock<IObjectIdConversionService> conversion = new();
        conversion.Setup(x => x.ConvertObjectIds(It.IsAny<string>())).Returns<string>(x => x);
        conversion.Setup(x => x.ConvertObjectId(It.IsAny<string>())).Returns<string>(x => x);
        return new UksfLoggerEventHandler(
            new EventBus(),
            _logs.Object,
            _auditLogs.Object,
            _errorLogs.Object,
            _launcherLogs.Object,
            _discordLogs.Object,
            new Mock<IUksfLogger>().Object,
            conversion.Object,
            new VerifyMode(verifyFlag, null),
            _output
        );
    }

    [Fact]
    public async Task InVerifyMode_LogsNeverReachTheDatabase()
    {
        var subject = CreateSubject("1");

        await subject.StoreAsync(new AuditLog("6ac666d72f2c765170a985be", "Email address confirmed for 6ac666d72f2c765170a985be"));
        await subject.StoreAsync(new ErrorLog(new System.Exception("boom"), "/x", "GET", "", 500, "", ""));
        await subject.StoreAsync(new DomainBasicLog("shutting down"));

        _auditLogs.Verify(x => x.Add(It.IsAny<AuditLog>()), Times.Never);
        _errorLogs.Verify(x => x.Add(It.IsAny<ErrorLog>()), Times.Never);
        _logs.Verify(x => x.Add(It.IsAny<DomainBasicLog>()), Times.Never);
        var lines = _output.ToString().Trim().Split('\n');
        lines.Should().HaveCount(3);
        lines[0].Should().StartWith("verify-log ").And.Contain("\"type\":\"AuditLog\"").And.Contain("Email address confirmed for 6ac666d72f2c765170a985be");
        lines[2].Should().Contain("shutting down");
    }

    [Fact]
    public async Task OutsideVerifyMode_LogsGoToTheDatabase()
    {
        var subject = CreateSubject(null);

        await subject.StoreAsync(new AuditLog("someone", "changed something"));
        await subject.StoreAsync(new DomainBasicLog("shutting down"));

        _auditLogs.Verify(x => x.Add(It.IsAny<AuditLog>()), Times.Once);
        _logs.Verify(x => x.Add(It.IsAny<DomainBasicLog>()), Times.Once);
        _output.ToString().Should().BeEmpty();
    }
}
