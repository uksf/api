using System;
using System.Collections.Generic;
using FluentAssertions;
using MongoDB.Bson;
using UKSF.Api.Personnel.Extensions;
using UKSF.Api.Personnel.Models;
using Xunit;

namespace UKSF.Tests.Unit.Services.Common {
    public class AccountUtilitiesTests {
        [Fact]
        public void ShouldCopyAccountCorrectly() {
            string id = ObjectId.GenerateNewId().ToString();
            DateTime timestamp = DateTime.Now.AddDays(-1);
            Account account = new() {
                Id = id,
                Firstname = "Bob",
                Lastname = "McTest",
                MembershipState = MembershipState.MEMBER,
                TeamspeakIdentities = new HashSet<double> { 4, 4 },
                ServiceRecord = new List<ServiceRecordEntry> { new() { Occurence = "Test", Timestamp = timestamp } },
                RolePreferences = new List<string> { "Aviation" },
                MilitaryExperience = false
            };

            PublicAccount subject = account.ToPublicAccount();

            subject.Id.Should().Be(id);
            subject.Firstname.Should().Be("Bob");
            subject.Lastname.Should().Be("McTest");
            subject.MembershipState.Should().Be(MembershipState.MEMBER);
            subject.TeamspeakIdentities.Should().NotBeEmpty().And.HaveCount(1).And.ContainInOrder(new[] { 4 });
            subject.ServiceRecord.Should().NotBeEmpty().And.HaveCount(1).And.OnlyContain(x => x.Occurence == "Test" && x.Timestamp == timestamp);
            subject.RolePreferences.Should().Contain("Aviation");
            subject.MilitaryExperience.Should().BeFalse();
        }

        [Fact]
        public void ShouldNotCopyPassword() {
            string id = ObjectId.GenerateNewId().ToString();
            Account account = new() { Id = id, Password = "thiswontappear" };

            PublicAccount subject = account.ToPublicAccount();

            subject.Id.Should().Be(id);
            subject.Password.Should().BeNull();
        }
    }
}
