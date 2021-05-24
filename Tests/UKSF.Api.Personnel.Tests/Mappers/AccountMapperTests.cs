using System;
using FluentAssertions;
using MongoDB.Bson;
using Moq;
using UKSF.Api.Personnel.Mappers;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using Xunit;

namespace UKSF.Api.Personnel.Tests.Mappers
{
    public class AccountMapperTests
    {
        private readonly Mock<IDisplayNameService> _mockDisplayNameService;
        private readonly AccountMapper _subject;

        public AccountMapperTests()
        {
            _mockDisplayNameService = new();

            _subject = new(_mockDisplayNameService.Object);
        }

        [Fact]
        public void ShouldCopyAccountCorrectly()
        {
            string id = ObjectId.GenerateNewId().ToString();
            DateTime timestamp = DateTime.Now.AddDays(-1);
            DomainAccount domainAccount = new()
            {
                Id = id,
                Firstname = "Bob",
                Lastname = "McTest",
                MembershipState = MembershipState.MEMBER,
                TeamspeakIdentities = new() { 4, 4 },
                ServiceRecord = new() { new() { Occurence = "Test", Timestamp = timestamp } },
                RolePreferences = new() { "Aviation" },
                MilitaryExperience = false
            };

            _mockDisplayNameService.Setup(x => x.GetDisplayName(domainAccount)).Returns("Cdt.McTest.B");

            Account subject = _subject.MapToAccount(domainAccount);

            subject.Id.Should().Be(id);
            subject.DisplayName.Should().Be("Cdt.McTest.B");
            subject.Firstname.Should().Be("Bob");
            subject.Lastname.Should().Be("McTest");
            subject.MembershipState.Should().Be(MembershipState.MEMBER);
            subject.TeamspeakIdentities.Should().NotBeEmpty().And.HaveCount(1).And.ContainInOrder(new[] { 4 });
            subject.ServiceRecord.Should().NotBeEmpty().And.HaveCount(1).And.OnlyContain(x => x.Occurence == "Test" && x.Timestamp == timestamp);
            subject.RolePreferences.Should().Contain("Aviation");
            subject.MilitaryExperience.Should().BeFalse();
        }
    }
}
