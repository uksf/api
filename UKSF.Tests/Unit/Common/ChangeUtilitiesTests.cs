using System;
using System.Collections.Generic;
using FluentAssertions;
using MongoDB.Bson;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Shared.Extensions;
using UKSF.Tests.Common;
using Xunit;

namespace UKSF.Tests.Unit.Common {
    public class ChangeUtilitiesTests {
        [Fact]
        public void Should_detect_changes_for_complex_object() {
            string id = ObjectId.GenerateNewId().ToString();
            Account original = new Account {
                id = id,
                firstname = "Tim",
                background = "I like trains",
                dob = DateTime.Parse("2018-08-09T05:00:00.307"),
                rank = "Private",
                application = new Application { state = ApplicationState.WAITING, recruiter = "Bob", applicationCommentThread = "thread1", dateCreated = DateTime.Parse("2020-05-02T10:34:39.786") },
                rolePreferences = new List<string> { "Aviatin", "NCO" }
            };
            Account updated = new Account {
                id = id,
                firstname = "Timmy",
                lastname = "Bob",
                background = "I like planes",
                dob = DateTime.Parse("2020-10-03T05:00:34.307"),
                application = new Application {
                    state = ApplicationState.ACCEPTED, recruiter = "Bob", dateCreated = DateTime.Parse("2020-05-02T10:34:39.786"), dateAccepted = DateTime.Parse("2020-07-02T10:34:39.786")
                },
                rolePreferences = new List<string> { "Aviation", "Officer" }
            };

            string subject = original.Changes(updated);

            subject.Should()
                   .Be(
                       "\n\t'lastname' added as 'Bob'" +
                       "\n\t'background' changed from 'I like trains' to 'I like planes'" +
                       "\n\t'dob' changed from '09/08/2018 05:00:00' to '03/10/2020 05:00:34'" +
                       "\n\t'firstname' changed from 'Tim' to 'Timmy'" +
                       "\n\t'rolePreferences' changed:" +
                       "\n\t\tadded: 'Aviation'" +
                       "\n\t\tadded: 'Officer'" +
                       "\n\t\tremoved: 'Aviatin'" +
                       "\n\t\tremoved: 'NCO'" +
                       "\n\t'rank' as 'Private' removed" +
                       "\n\t'application' changed:" +
                       "\n\t\t'dateAccepted' changed from '01/01/0001 00:00:00' to '02/07/2020 10:34:39'" +
                       "\n\t\t'state' changed from 'WAITING' to 'ACCEPTED'" +
                       "\n\t\t'applicationCommentThread' as 'thread1' removed"
                   );
        }

        [Fact]
        public void Should_detect_changes_for_date() {
            string id = ObjectId.GenerateNewId().ToString();
            Account original = new Account { id = id, dob = DateTime.Parse("2020-10-03T05:00:34.307") };
            Account updated = new Account { id = id, dob = DateTime.Parse("2020-11-03T00:00:00.000") };

            string subject = original.Changes(updated);

            subject.Should().Be("\n\t'dob' changed from '03/10/2020 05:00:34' to '03/11/2020 00:00:00'");
        }

        [Fact]
        public void Should_detect_changes_for_dictionary() {
            string id = ObjectId.GenerateNewId().ToString();
            TestDataModel original = new TestDataModel { id = id, Dictionary = new Dictionary<string, object> { { "0", "variable0" }, { "1", "variable0" } } };
            TestDataModel updated = new TestDataModel { id = id, Dictionary = new Dictionary<string, object> { { "0", "variable0" }, { "1", "variable1" }, { "2", "variable2" } } };

            string subject = original.Changes(updated);

            subject.Should().Be("\n\t'Dictionary' changed:" + "\n\t\tadded: '[1, variable1]'" + "\n\t\tadded: '[2, variable2]'" + "\n\t\tremoved: '[1, variable0]'");
        }

        [Fact]
        public void Should_detect_changes_for_enum() {
            string id = ObjectId.GenerateNewId().ToString();
            Account original = new Account { id = id, membershipState = MembershipState.UNCONFIRMED };
            Account updated = new Account { id = id, membershipState = MembershipState.MEMBER };

            string subject = original.Changes(updated);

            subject.Should().Be("\n\t'membershipState' changed from 'UNCONFIRMED' to 'MEMBER'");
        }

        [Fact]
        public void Should_detect_changes_for_hashset() {
            string id = ObjectId.GenerateNewId().ToString();
            Account original = new Account { id = id, teamspeakIdentities = new HashSet<double> { 0 } };
            Account updated = new Account { id = id, teamspeakIdentities = new HashSet<double> { 0, 1, 2, 2 } };

            string subject = original.Changes(updated);

            subject.Should().Be("\n\t'teamspeakIdentities' changed:" + "\n\t\tadded: '1'" + "\n\t\tadded: '2'");
        }

        [Fact]
        public void Should_detect_changes_for_object_list() {
            string id = ObjectId.GenerateNewId().ToString();
            Account original = new Account { id = id, serviceRecord = new List<ServiceRecordEntry> { new ServiceRecordEntry { Occurence = "Event" } } };
            Account updated = new Account {
                id = id, serviceRecord = new List<ServiceRecordEntry> { new ServiceRecordEntry { Occurence = "Event" }, new ServiceRecordEntry { Occurence = "Another Event" } }
            };

            string subject = original.Changes(updated);

            subject.Should().Be("\n\t'serviceRecord' changed:" + "\n\t\tadded: '01/01/0001: Another Event'");
        }

        [Fact]
        public void Should_detect_changes_for_simple_list() {
            string id = ObjectId.GenerateNewId().ToString();
            Account original = new Account { id = id, rolePreferences = new List<string> { "Aviatin", "NCO" } };
            Account updated = new Account { id = id, rolePreferences = new List<string> { "Aviation", "NCO", "Officer" } };

            string subject = original.Changes(updated);

            subject.Should().Be("\n\t'rolePreferences' changed:" + "\n\t\tadded: 'Aviation'" + "\n\t\tadded: 'Officer'" + "\n\t\tremoved: 'Aviatin'");
        }

        [Fact]
        public void Should_detect_changes_for_simple_object() {
            string id = ObjectId.GenerateNewId().ToString();
            Rank original = new Rank { id = id, abbreviation = "Pte", name = "Privte", order = 1 };
            Rank updated = new Rank { id = id, name = "Private", order = 5, teamspeakGroup = "4" };

            string subject = original.Changes(updated);

            subject.Should().Be("\n\t'teamspeakGroup' added as '4'" + "\n\t'name' changed from 'Privte' to 'Private'" + "\n\t'order' changed from '1' to '5'" + "\n\t'abbreviation' as 'Pte' removed");
        }

        [Fact]
        public void Should_do_nothing_when_null() {
            string subject = ((Rank) null).Changes(null);

            subject.Should().Be("No changes");
        }

        [Fact]
        public void Should_do_nothing_when_field_is_null() {
            string id = ObjectId.GenerateNewId().ToString();
            Rank original = new Rank { id = id, abbreviation = null };
            Rank updated = new Rank { id = id, abbreviation = null };

            string subject = original.Changes(updated);

            subject.Should().Be("No changes");
        }

        [Fact]
        public void Should_do_nothing_when_objects_are_equal() {
            string id = ObjectId.GenerateNewId().ToString();
            Rank original = new Rank { id = id, abbreviation = "Pte" };
            Rank updated = new Rank { id = id, abbreviation = "Pte" };

            string subject = original.Changes(updated);

            subject.Should().Be("No changes");
        }
    }
}
