using System;
using System.Collections.Generic;
using FluentAssertions;
using MongoDB.Bson;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Tests.Common;
using Xunit;

namespace UKSF.Tests.Unit.Common;

public class ChangeUtilitiesTests
{
    [Fact]
    public void Should_detect_changes_for_complex_object()
    {
        var id = ObjectId.GenerateNewId().ToString();
        DateTime dobBefore = new(2020, 10, 3, 5, 34, 0);
        DateTime dobAfter = new(2020, 11, 3);
        DateTime dateAccepted = new(2020, 7, 2, 10, 34, 39);
        DomainAccount original = new()
        {
            Id = id,
            Firstname = "Tim",
            Background = "I like trains",
            Dob = dobBefore,
            Rank = "Private",
            Application = new()
            {
                State = ApplicationState.WAITING, Recruiter = "Bob", ApplicationCommentThread = "thread1", DateCreated = new(2020, 5, 2, 10, 34, 39)
            },
            RolePreferences = new() { "Aviatin", "NCO" }
        };
        DomainAccount updated = new()
        {
            Id = id,
            Firstname = "Timmy",
            Lastname = "Bob",
            Background = "I like planes",
            Dob = dobAfter,
            Application = new()
            {
                State = ApplicationState.ACCEPTED, Recruiter = "Bob", DateCreated = new(2020, 5, 2, 10, 34, 39), DateAccepted = dateAccepted
            },
            RolePreferences = new() { "Aviation", "Officer" }
        };

        var subject = original.Changes(updated);

        subject.Should()
               .Be(
                   "\n\t'Lastname' added as 'Bob'" +
                   "\n\t'Background' changed from 'I like trains' to 'I like planes'" +
                   $"\n\t'Dob' changed from '{dobBefore}' to '{dobAfter}'" +
                   "\n\t'Firstname' changed from 'Tim' to 'Timmy'" +
                   "\n\t'RolePreferences' changed:" +
                   "\n\t\tadded: 'Aviation'" +
                   "\n\t\tadded: 'Officer'" +
                   "\n\t\tremoved: 'Aviatin'" +
                   "\n\t\tremoved: 'NCO'" +
                   "\n\t'Rank' as 'Private' removed" +
                   "\n\t'Application' changed:" +
                   $"\n\t\t'DateAccepted' changed from '{new DateTime()}' to '{dateAccepted}'" +
                   "\n\t\t'State' changed from 'WAITING' to 'ACCEPTED'" +
                   "\n\t\t'ApplicationCommentThread' as 'thread1' removed"
               );
    }

    [Fact]
    public void Should_detect_changes_for_date()
    {
        var id = ObjectId.GenerateNewId().ToString();
        DateTime dobBefore = new(2020, 10, 3, 5, 34, 0);
        DateTime dobAfter = new(2020, 11, 3);
        DomainAccount original = new() { Id = id, Dob = dobBefore };
        DomainAccount updated = new() { Id = id, Dob = dobAfter };

        var subject = original.Changes(updated);

        subject.Should().Be($"\n\t'Dob' changed from '{dobBefore}' to '{dobAfter}'");
    }

    [Fact]
    public void Should_detect_changes_for_dictionary()
    {
        var id = ObjectId.GenerateNewId().ToString();
        TestDataModel original = new() { Id = id, Dictionary = new() { { "0", "variable0" }, { "1", "variable0" } } };
        TestDataModel updated = new() { Id = id, Dictionary = new() { { "0", "variable0" }, { "1", "variable1" }, { "2", "variable2" } } };

        var subject = original.Changes(updated);

        subject.Should()
               .Be("\n\t'Dictionary' changed:" + "\n\t\tadded: '[1, variable1]'" + "\n\t\tadded: '[2, variable2]'" + "\n\t\tremoved: '[1, variable0]'");
    }

    [Fact]
    public void Should_detect_changes_for_enum()
    {
        var id = ObjectId.GenerateNewId().ToString();
        DomainAccount original = new() { Id = id, MembershipState = MembershipState.UNCONFIRMED };
        DomainAccount updated = new() { Id = id, MembershipState = MembershipState.MEMBER };

        var subject = original.Changes(updated);

        subject.Should().Be("\n\t'MembershipState' changed from 'UNCONFIRMED' to 'MEMBER'");
    }

    [Fact]
    public void Should_detect_changes_for_hashset()
    {
        var id = ObjectId.GenerateNewId().ToString();
        DomainAccount original = new() { Id = id, TeamspeakIdentities = new() { 0 } };
        DomainAccount updated = new() { Id = id, TeamspeakIdentities = new() { 0, 1, 2, 2 } };

        var subject = original.Changes(updated);

        subject.Should().Be("\n\t'TeamspeakIdentities' changed:" + "\n\t\tadded: '1'" + "\n\t\tadded: '2'");
    }

    [Fact]
    public void Should_detect_changes_for_object_list()
    {
        var id = ObjectId.GenerateNewId().ToString();
        DomainAccount original = new() { Id = id, ServiceRecord = new() { new() { Occurence = "Event" } } };
        DomainAccount updated = new() { Id = id, ServiceRecord = new() { new() { Occurence = "Event" }, new() { Occurence = "Another Event" } } };

        var subject = original.Changes(updated);

        subject.Should().Be("\n\t'ServiceRecord' changed:" + "\n\t\tadded: '01/01/0001: Another Event'");
    }

    [Fact]
    public void Should_detect_changes_for_simple_list()
    {
        List<string> original = new() { "Aviatin", "NCO" };
        List<string> updated = new() { "Aviation", "NCO", "Officer" };

        var subject = original.Changes(updated);

        subject.Should().Be("\n\t'List' changed:" + "\n\t\tadded: 'Aviation'" + "\n\t\tadded: 'Officer'" + "\n\t\tremoved: 'Aviatin'");
    }

    [Fact]
    public void Should_detect_changes_for_simple_object()
    {
        var id = ObjectId.GenerateNewId().ToString();
        DomainRank original = new() { Id = id, Abbreviation = "Pte", Name = "Privte", Order = 1 };
        DomainRank updated = new() { Id = id, Name = "Private", Order = 5, TeamspeakGroup = "4" };

        var subject = original.Changes(updated);

        subject.Should()
               .Be(
                   "\n\t'TeamspeakGroup' added as '4'" +
                   "\n\t'Name' changed from 'Privte' to 'Private'" +
                   "\n\t'Order' changed from '1' to '5'" +
                   "\n\t'Abbreviation' as 'Pte' removed"
               );
    }

    [Fact]
    public void Should_detect_changes_for_simple_object_with_list()
    {
        var id = ObjectId.GenerateNewId().ToString();
        DomainAccount original = new() { Id = id, RolePreferences = new() { "Aviatin", "NCO" } };
        DomainAccount updated = new() { Id = id, RolePreferences = new() { "Aviation", "NCO", "Officer" } };

        var subject = original.Changes(updated);

        subject.Should().Be("\n\t'RolePreferences' changed:" + "\n\t\tadded: 'Aviation'" + "\n\t\tadded: 'Officer'" + "\n\t\tremoved: 'Aviatin'");
    }

    [Fact]
    public void Should_do_nothing_when_field_is_null()
    {
        var id = ObjectId.GenerateNewId().ToString();
        DomainRank original = new() { Id = id, Abbreviation = null };
        DomainRank updated = new() { Id = id, Abbreviation = null };

        var subject = original.Changes(updated);

        subject.Should().Be("\tNo changes");
    }

    [Fact]
    public void Should_do_nothing_when_null()
    {
        var subject = ((DomainRank)null).Changes(null);

        subject.Should().Be("\tNo changes");
    }

    [Fact]
    public void Should_do_nothing_when_objects_are_equal()
    {
        var id = ObjectId.GenerateNewId().ToString();
        DomainRank original = new() { Id = id, Abbreviation = "Pte" };
        DomainRank updated = new() { Id = id, Abbreviation = "Pte" };

        var subject = original.Changes(updated);

        subject.Should().Be("\tNo changes");
    }
}
