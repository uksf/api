using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.Core.Commands;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Models.Domain;
using Xunit;

namespace UKSF.Api.Core.Tests.Commands;

public class BulkUpdateAccountTrainingCommandTests
{
    private readonly Mock<IAccountContext> _mockAccountContext = new();
    private readonly Mock<ITrainingsContext> _mockTrainingsContext = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly UpdateAccountTrainingCommandHandler _subject;

    public BulkUpdateAccountTrainingCommandTests()
    {
        _mockTrainingsContext.Setup(x => x.Get())
                             .Returns(
                                 [
                                     new DomainTraining { Id = "t1", Name = "Training 1" },
                                     new DomainTraining { Id = "t2", Name = "Training 2" },
                                     new DomainTraining { Id = "t3", Name = "Training 3" }
                                 ]
                             );
        _subject = new UpdateAccountTrainingCommandHandler(_mockAccountContext.Object, _mockTrainingsContext.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Add_merges_trainings_into_each_account()
    {
        GivenAccount("a1", "t1");
        GivenAccount("a2");

        await _subject.ExecuteAsync(new BulkUpdateAccountTrainingCommand(["a1", "a2"], ["t1", "t2"], false));

        VerifyUpdated("a1", ["t1", "t2"]);
        VerifyUpdated("a2", ["t1", "t2"]);
    }

    [Fact]
    public async Task Remove_strips_trainings_from_each_account()
    {
        GivenAccount("a1", "t1", "t2");
        GivenAccount("a2", "t2", "t3");

        await _subject.ExecuteAsync(new BulkUpdateAccountTrainingCommand(["a1", "a2"], ["t2"], true));

        VerifyUpdated("a1", ["t1"]);
        VerifyUpdated("a2", ["t3"]);
    }

    [Fact]
    public async Task Accounts_without_a_change_are_not_updated()
    {
        GivenAccount("a1", "t1");

        await _subject.ExecuteAsync(new BulkUpdateAccountTrainingCommand(["a1"], ["t2"], true));

        _mockAccountContext.Verify(
            x => x.Update(It.IsAny<string>(), It.IsAny<Expression<Func<DomainAccount, List<string>>>>(), It.IsAny<List<string>>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Unknown_account_throws_before_any_update()
    {
        GivenAccount("a1");

        var act = () => _subject.ExecuteAsync(new BulkUpdateAccountTrainingCommand(["a1", "missing"], ["t1"], false));

        await act.Should().ThrowAsync<BadRequestException>();
        _mockAccountContext.Verify(
            x => x.Update(It.IsAny<string>(), It.IsAny<Expression<Func<DomainAccount, List<string>>>>(), It.IsAny<List<string>>()),
            Times.Never
        );
    }

    private void GivenAccount(string id, params string[] trainings)
    {
        _mockAccountContext.Setup(x => x.GetSingle(id)).Returns(new DomainAccount { Id = id, Trainings = [..trainings] });
    }

    private void VerifyUpdated(string id, List<string> expected)
    {
        _mockAccountContext.Verify(
            x => x.Update(
                id,
                It.IsAny<Expression<Func<DomainAccount, List<string>>>>(),
                It.Is<List<string>>(actual => actual.Count == expected.Count && !expected.Exists(e => !actual.Contains(e)))
            ),
            Times.Once
        );
    }
}
