using System;
using System.Collections.Generic;
using FluentAssertions;
using MongoDB.Bson;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Shared.Extensions;
using UKSF.Api.Tests.Common;
using Xunit;

namespace UKSF.Tests.Unit.Common {
    public class ChangeUtilitiesTests {
        [Fact]
        public void Should_detect_changes_for_complex_object() {
            string id = ObjectId.GenerateNewId().ToString();
            Account original = new() {
                Id = id,
                Firstname = "Tim",
                Background = "I like trains",
                Dob = DateTime.Parse("2018-08-09T05:00:00.307"),
                Rank = "Private",
                Application = new Application { State = ApplicationState.WAITING, Recruiter = "Bob", ApplicationCommentThread = "thread1", DateCreated = DateTime.Parse("2020-05-02T10:34:39.786") },
                RolePreferences = new List<string> { "Aviatin", "NCO" }
            };
            Account updated = new() {
                Id = id,
                Firstname = "Timmy",
                Lastname = "Bob",
                Background = "I like planes",
                Dob = DateTime.Parse("2020-10-03T05:00:34.307"),
                Application = new Application {
                    State = ApplicationState.ACCEPTED, Recruiter = "Bob", DateCreated = DateTime.Parse("2020-05-02T10:34:39.786"), DateAccepted = DateTime.Parse("2020-07-02T10:34:39.786")
                },
                RolePreferences = new List<string> { "Aviation", "Officer" }
            };

            string subject = original.Changes(updated);

            subject.Should()
                   .Be(
                       "\n\t'Lastname' added as 'Bob'" +
                       "\n\t'Background' changed from 'I like trains' to 'I like planes'" +
                       "\n\t'Dob' changed from '09/08/2018 05:00:00' to '03/10/2020 05:00:34'" +
                       "\n\t'Firstname' changed from 'Tim' to 'Timmy'" +
                       "\n\t'RolePreferences' changed:" +
                       "\n\t\tadded: 'Aviation'" +
                       "\n\t\tadded: 'Officer'" +
                       "\n\t\tremoved: 'Aviatin'" +
                       "\n\t\tremoved: 'NCO'" +
                       "\n\t'Rank' as 'Private' removed" +
                       "\n\t'Application' changed:" +
                       "\n\t\t'DateAccepted' changed from '01/01/0001 00:00:00' to '02/07/2020 10:34:39'" +
                       "\n\t\t'State' changed from 'WAITING' to 'ACCEPTED'" +
                       "\n\t\t'ApplicationCommentThread' as 'thread1' removed"
                   );
        }

        [Fact]
        public void Should_detect_changes_for_date() {
            string id = ObjectId.GenerateNewId().ToString();
            Account original = new() { Id = id, Dob = DateTime.Parse("2020-10-03T05:00:34.307") };
            Account updated = new() { Id = id, Dob = DateTime.Parse("2020-11-03T00:00:00.000") };

            string subject = original.Changes(updated);

            subject.Should().Be("\n\t'Dob' changed from '03/10/2020 05:00:34' to '03/11/2020 00:00:00'");
        }

        [Fact]
        public void Should_detect_changes_for_dictionary() {
            string id = ObjectId.GenerateNewId().ToString();
            TestDataModel original = new() { Id = id, Dictionary = new Dictionary<string, object> { { "0", "variable0" }, { "1", "variable0" } } };
            TestDataModel updated = new() { Id = id, Dictionary = new Dictionary<string, object> { { "0", "variable0" }, { "1", "variable1" }, { "2", "variable2" } } };

            string subject = original.Changes(updated);

            subject.Should().Be("\n\t'Dictionary' changed:" + "\n\t\tadded: '[1, variable1]'" + "\n\t\tadded: '[2, variable2]'" + "\n\t\tremoved: '[1, variable0]'");
        }

        [Fact]
        public void Should_detect_changes_for_enum() {
            string id = ObjectId.GenerateNewId().ToString();
            Account original = new() { Id = id, MembershipState = MembershipState.UNCONFIRMED };
            Account updated = new() { Id = id, MembershipState = MembershipState.MEMBER };

            string subject = original.Changes(updated);

            subject.Should().Be("\n\t'MembershipState' changed from 'UNCONFIRMED' to 'MEMBER'");
        }

        [Fact]
        public void Should_detect_changes_for_hashset() {
            string id = ObjectId.GenerateNewId().ToString();
            Account original = new() { Id = id, TeamspeakIdentities = new HashSet<double> { 0 } };
            Account updated = new() { Id = id, TeamspeakIdentities = new HashSet<double> { 0, 1, 2, 2 } };

            string subject = original.Changes(updated);

            subject.Should().Be("\n\t'TeamspeakIdentities' changed:" + "\n\t\tadded: '1'" + "\n\t\tadded: '2'");
        }

        [Fact]
        public void Should_detect_changes_for_object_list() {
            string id = ObjectId.GenerateNewId().ToString();
            Account original = new() { Id = id, ServiceRecord = new List<ServiceRecordEntry> { new() { Occurence = "Event" } } };
            Account updated = new() { Id = id, ServiceRecord = new List<ServiceRecordEntry> { new() { Occurence = "Event" }, new() { Occurence = "Another Event" } } };

            string subject = original.Changes(updated);

            subject.Should().Be("\n\t'ServiceRecord' changed:" + "\n\t\tadded: '01/01/0001: Another Event'");
        }

        [Fact]
        public void Should_detect_changes_for_simple_object_with_list() {
            string id = ObjectId.GenerateNewId().ToString();
            Account original = new() { Id = id, RolePreferences = new List<string> { "Aviatin", "NCO" } };
            Account updated = new() { Id = id, RolePreferences = new List<string> { "Aviation", "NCO", "Officer" } };

            string subject = original.Changes(updated);

            subject.Should().Be("\n\t'RolePreferences' changed:" + "\n\t\tadded: 'Aviation'" + "\n\t\tadded: 'Officer'" + "\n\t\tremoved: 'Aviatin'");
        }

        [Fact]
        public void Should_detect_changes_for_simple_object() {
            string id = ObjectId.GenerateNewId().ToString();
            Rank original = new() { Id = id, Abbreviation = "Pte", Name = "Privte", Order = 1 };
            Rank updated = new() { Id = id, Name = "Private", Order = 5, TeamspeakGroup = "4" };

            string subject = original.Changes(updated);

            subject.Should().Be("\n\t'TeamspeakGroup' added as '4'" + "\n\t'Name' changed from 'Privte' to 'Private'" + "\n\t'Order' changed from '1' to '5'" + "\n\t'Abbreviation' as 'Pte' removed");
        }

        [Fact]
        public void Should_do_nothing_when_field_is_null() {
            string id = ObjectId.GenerateNewId().ToString();
            Rank original = new() { Id = id, Abbreviation = null };
            Rank updated = new() { Id = id, Abbreviation = null };

            string subject = original.Changes(updated);

            subject.Should().Be("\tNo changes");
        }

        [Fact]
        public void Should_do_nothing_when_null() {
            string subject = ((Rank) null).Changes(null);

            subject.Should().Be("\tNo changes");
        }

        [Fact]
        public void Should_do_nothing_when_objects_are_equal() {
            string id = ObjectId.GenerateNewId().ToString();
            Rank original = new() { Id = id, Abbreviation = "Pte" };
            Rank updated = new() { Id = id, Abbreviation = "Pte" };

            string subject = original.Changes(updated);

            subject.Should().Be("\tNo changes");
        }

        [Fact]
        public void Should_detect_changes_for_simple_list() {
            List<string> original = new() { "Aviatin", "NCO" };
            List<string> updated = new() { "Aviation", "NCO", "Officer" };

            string subject = original.Changes(updated);

            subject.Should().Be("\n\t'List' changed:" + "\n\t\tadded: 'Aviation'" + "\n\t\tadded: 'Officer'" + "\n\t\tremoved: 'Aviatin'");
        }
    }
}
