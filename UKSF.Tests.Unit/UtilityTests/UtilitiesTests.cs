using System;
using System.Collections.Generic;
using FluentAssertions;
using MongoDB.Bson;
using UKSF.Api.Models.Personnel;
using UKSF.Api.Services.Utility;
using Xunit;

namespace UKSF.Tests.Unit.UtilityTests {
    public class UtilitiesTests {
        [Theory, InlineData(25, 4, 25, 4), InlineData(25, 13, 26, 1)]
        public void ShouldGiveCorrectAge(int years, int months, int expectedYears, int expectedMonths) {
            DateTime dob = DateTime.Today.AddYears(-years).AddMonths(-months);

            (int subjectYears, int subjectMonths) = dob.ToAge();

            subjectYears.Should().Be(expectedYears);
            subjectMonths.Should().Be(expectedMonths);
        }

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
        public void ShouldGiveCorrectMonths() {
            DateTime dob = new DateTime(2019, 1, 20);

            (int _, int subjectMonths) = dob.ToAge(new DateTime(2020, 1, 16));

            subjectMonths.Should().Be(11);
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
