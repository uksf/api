using System;
using System.Collections.Generic;
using FluentAssertions;
using MongoDB.Bson;
using UKSF.Api.Personnel.Models;
using Xunit;

namespace UKSF.Tests.Unit.Services.Common {
    public class AccountUtilitiesTests {
        [Fact]
        public void ShouldCopyAccountCorrectly() {
            string id = ObjectId.GenerateNewId().ToString();
            DateTime timestamp = DateTime.Now.AddDays(-1);
            Account account = new Account {
                id = id,
                firstname = "Bob",
                lastname = "McTest",
                membershipState = MembershipState.MEMBER,
                teamspeakIdentities = new HashSet<double> {4, 4},
                serviceRecord = new List<ServiceRecordEntry> {new ServiceRecordEntry {occurence = "Test", timestamp = timestamp}},
                rolePreferences = new List<string> {"Aviation"},
                militaryExperience = false
            };

            PublicAccount subject = account.ToPublicAccount();

            subject.id.Should().Be(id);
            subject.firstname.Should().Be("Bob");
            subject.lastname.Should().Be("McTest");
            subject.membershipState.Should().Be(MembershipState.MEMBER);
            subject.teamspeakIdentities.Should().NotBeEmpty().And.HaveCount(1).And.ContainInOrder(new[] {4});
            subject.serviceRecord.Should().NotBeEmpty().And.HaveCount(1).And.OnlyContain(x => x.occurence == "Test" && x.timestamp == timestamp);
            subject.rolePreferences.Should().Contain("Aviation");
            subject.militaryExperience.Should().BeFalse();
        }

        [Fact]
        public void ShouldNotCopyPassword() {
            string id = ObjectId.GenerateNewId().ToString();
            Account account = new Account {id = id, password = "thiswontappear"};

            PublicAccount subject = account.ToPublicAccount();

            subject.id.Should().Be(id);
            subject.password.Should().BeNull();
        }
    }
}
