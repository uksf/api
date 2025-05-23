using System;
using FluentAssertions;
using MongoDB.Bson;
using Moq;
using UKSF.Api.Core.Mappers;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.Tests.Mappers;

public class AccountMapperTests
{
    private readonly Mock<IDisplayNameService> _mockDisplayNameService;
    private readonly AccountMapper _subject;

    public AccountMapperTests()
    {
        _mockDisplayNameService = new Mock<IDisplayNameService>();

        _subject = new AccountMapper(_mockDisplayNameService.Object);
    }

    [Fact]
    public void ShouldCopyAccountCorrectly()
    {
        var id = ObjectId.GenerateNewId().ToString();
        var timestamp = DateTime.UtcNow.AddDays(-1);
        DomainAccount account = new()
        {
            Id = id,
            Firstname = "Bob",
            Lastname = "McTest",
            MembershipState = MembershipState.Member,
            TeamspeakIdentities = [4],
            ServiceRecord = [new ServiceRecordEntry { Occurence = "Test", Timestamp = timestamp }],
            RolePreferences = ["Aviation"],
            MilitaryExperience = false
        };

        _mockDisplayNameService.Setup(x => x.GetDisplayName(account)).Returns("Cdt.McTest.B");

        var subject = _subject.MapToAccount(account);

        subject.Id.Should().Be(id);
        subject.DisplayName.Should().Be("Cdt.McTest.B");
        subject.Firstname.Should().Be("Bob");
        subject.Lastname.Should().Be("McTest");
        subject.MembershipState.Should().Be(MembershipState.Member);
        subject.TeamspeakIdentities.Should().NotBeEmpty().And.HaveCount(1).And.ContainInOrder(4);
        subject.ServiceRecord.Should().NotBeEmpty().And.HaveCount(1).And.OnlyContain(x => x.Occurence == "Test" && x.Timestamp == timestamp);
        subject.RolePreferences.Should().Contain("Aviation");
        subject.MilitaryExperience.Should().BeFalse();
    }
}
