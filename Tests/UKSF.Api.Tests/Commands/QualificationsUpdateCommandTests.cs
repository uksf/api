using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Moq;
using UKSF.Api.Commands;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;
using Xunit;

namespace UKSF.Api.Tests.Commands;

public class QualificationsUpdateCommandTests
{
    private const string AccountId = "testAccountId";
    private readonly Mock<IAccountContext> _mockAccountContext;
    private readonly Mock<IUksfLogger> _mockLogger;
    private readonly QualificationsUpdateCommand _subject;

    public QualificationsUpdateCommandTests()
    {
        _mockAccountContext = new Mock<IAccountContext>();
        _mockLogger = new Mock<IUksfLogger>();
        _subject = new QualificationsUpdateCommand(_mockAccountContext.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task ExecuteAsync_updates_medic_when_changed()
    {
        GivenAccountWithQualifications(medic: false, engineer: false);

        await _subject.ExecuteAsync(AccountId, new Qualifications { Medic = true, Engineer = false });

        _mockAccountContext.Verify(x => x.Update(AccountId, It.IsAny<Expression<Func<DomainAccount, bool>>>(), true), Times.Once);
        _mockLogger.Verify(x => x.LogAudit(It.Is<string>(s => s.Contains("Medic") && s.Contains("enabled")), null), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_updates_engineer_when_changed()
    {
        GivenAccountWithQualifications(medic: false, engineer: false);

        await _subject.ExecuteAsync(AccountId, new Qualifications { Medic = false, Engineer = true });

        _mockAccountContext.Verify(x => x.Update(AccountId, It.IsAny<Expression<Func<DomainAccount, bool>>>(), true), Times.Once);
        _mockLogger.Verify(x => x.LogAudit(It.Is<string>(s => s.Contains("Engineer") && s.Contains("enabled")), null), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_does_not_update_medic_when_unchanged()
    {
        GivenAccountWithQualifications(medic: false, engineer: false);

        await _subject.ExecuteAsync(AccountId, new Qualifications { Medic = false, Engineer = false });

        _mockAccountContext.Verify(x => x.Update(It.IsAny<string>(), It.IsAny<Expression<Func<DomainAccount, bool>>>(), It.IsAny<bool>()), Times.Never);
        _mockLogger.Verify(x => x.LogAudit(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_does_not_update_engineer_when_unchanged()
    {
        GivenAccountWithQualifications(medic: true, engineer: true);

        await _subject.ExecuteAsync(AccountId, new Qualifications { Medic = true, Engineer = true });

        _mockAccountContext.Verify(x => x.Update(It.IsAny<string>(), It.IsAny<Expression<Func<DomainAccount, bool>>>(), It.IsAny<bool>()), Times.Never);
        _mockLogger.Verify(x => x.LogAudit(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_updates_both_when_both_changed()
    {
        GivenAccountWithQualifications(medic: false, engineer: true);

        await _subject.ExecuteAsync(AccountId, new Qualifications { Medic = true, Engineer = false });

        _mockAccountContext.Verify(x => x.Update(AccountId, It.IsAny<Expression<Func<DomainAccount, bool>>>(), true), Times.Once);
        _mockAccountContext.Verify(x => x.Update(AccountId, It.IsAny<Expression<Func<DomainAccount, bool>>>(), false), Times.Once);
        _mockLogger.Verify(x => x.LogAudit(It.Is<string>(s => s.Contains("Medic") && s.Contains("enabled")), null), Times.Once);
        _mockLogger.Verify(x => x.LogAudit(It.Is<string>(s => s.Contains("Engineer") && s.Contains("disabled")), null), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_updates_neither_when_neither_changed()
    {
        GivenAccountWithQualifications(medic: true, engineer: false);

        await _subject.ExecuteAsync(AccountId, new Qualifications { Medic = true, Engineer = false });

        _mockAccountContext.Verify(x => x.Update(It.IsAny<string>(), It.IsAny<Expression<Func<DomainAccount, bool>>>(), It.IsAny<bool>()), Times.Never);
        _mockLogger.Verify(x => x.LogAudit(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    private void GivenAccountWithQualifications(bool medic, bool engineer)
    {
        _mockAccountContext.Setup(x => x.GetSingle(AccountId))
                           .Returns(new DomainAccount { Id = AccountId, Qualifications = new Qualifications { Medic = medic, Engineer = engineer } });
    }
}
