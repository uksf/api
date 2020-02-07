using System;
using System.Collections.Generic;
using FluentAssertions;
using MongoDB.Bson;
using UKSF.Api.Models.Personnel;
using UKSF.Api.Services.Common;
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
                serviceRecord = new[] {new ServiceRecordEntry {occurence = "Test", timestamp = timestamp}},
                aviation = true,
                militaryExperience = false
            };

            ExtendedAccount subject = account.ToExtendedAccount();

            subject.id.Should().Be(id);
            subject.firstname.Should().Be("Bob");
            subject.lastname.Should().Be("McTest");
            subject.membershipState.Should().Be(MembershipState.MEMBER);
            subject.teamspeakIdentities.Should().NotBeEmpty().And.HaveCount(1).And.ContainInOrder(new[] {4});
            subject.serviceRecord.Should().NotBeEmpty().And.HaveCount(1).And.OnlyContain(x => x.occurence == "Test" && x.timestamp == timestamp);
            subject.aviation.Should().BeTrue();
            subject.militaryExperience.Should().BeFalse();
        }

        [Fact]
        public void ShouldNotCopyPassword() {
            string id = ObjectId.GenerateNewId().ToString();
            Account account = new Account {id = id, password = "thiswontappear"};

            ExtendedAccount subject = account.ToExtendedAccount();

            subject.id.Should().Be(id);
            subject.password.Should().BeNull();
        }
    }
}
