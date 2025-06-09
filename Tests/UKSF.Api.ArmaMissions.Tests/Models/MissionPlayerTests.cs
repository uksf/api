using FluentAssertions;
using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.Core.Models.Domain;
using Xunit;

namespace UKSF.Api.ArmaMissions.Tests.Models;

public class MissionPlayerTests
{
    [Fact]
    public void ShouldInitializeWithDefaultValues()
    {
        // Act
        var subject = new MissionPlayer();

        // Assert
        subject.Account.Should().BeNull();
        subject.Name.Should().BeNull();
        subject.ObjectClass.Should().BeNull();
        subject.Rank.Should().BeNull();
        subject.Unit.Should().BeNull();
    }

    [Fact]
    public void ShouldAllowSettingName()
    {
        // Arrange
        var subject = new MissionPlayer();
        const string expectedName = "John Doe";

        // Act
        subject.Name = expectedName;

        // Assert
        subject.Name.Should().Be(expectedName);
    }

    [Fact]
    public void ShouldAllowSettingObjectClass()
    {
        // Arrange
        var subject = new MissionPlayer();
        const string expectedObjectClass = "UKSF_B_Rifleman";

        // Act
        subject.ObjectClass = expectedObjectClass;

        // Assert
        subject.ObjectClass.Should().Be(expectedObjectClass);
    }

    [Fact]
    public void ShouldAllowSettingAccount()
    {
        // Arrange
        var subject = new MissionPlayer();
        var account = new DomainAccount
        {
            Id = "account123",
            Firstname = "John",
            Lastname = "Doe"
        };

        // Act
        subject.Account = account;

        // Assert
        subject.Account.Should().Be(account);
        subject.Account.Id.Should().Be("account123");
        subject.Account.Firstname.Should().Be("John");
        subject.Account.Lastname.Should().Be("Doe");
    }

    [Fact]
    public void ShouldAllowSettingRank()
    {
        // Arrange
        var subject = new MissionPlayer();
        var rank = new DomainRank
        {
            Id = "rank123",
            Name = "Sergeant",
            Abbreviation = "Sgt"
        };

        // Act
        subject.Rank = rank;

        // Assert
        subject.Rank.Should().Be(rank);
        subject.Rank.Id.Should().Be("rank123");
        subject.Rank.Name.Should().Be("Sergeant");
        subject.Rank.Abbreviation.Should().Be("Sgt");
    }

    [Fact]
    public void ShouldAllowSettingUnit()
    {
        // Arrange
        var subject = new MissionPlayer();
        var unit = new MissionUnit { Callsign = "Alpha-1" };

        // Act
        subject.Unit = unit;

        // Assert
        subject.Unit.Should().Be(unit);
        subject.Unit.Callsign.Should().Be("Alpha-1");
    }

    [Fact]
    public void ShouldAllowSettingAllPropertiesTogether()
    {
        // Arrange
        var subject = new MissionPlayer();
        var account = new DomainAccount { Id = "account123", Firstname = "John" };
        var rank = new DomainRank { Name = "Private" };
        var unit = new MissionUnit { Callsign = "Bravo-2" };

        // Act
        subject.Name = "John Doe";
        subject.ObjectClass = "UKSF_B_Medic";
        subject.Account = account;
        subject.Rank = rank;
        subject.Unit = unit;

        // Assert
        subject.Name.Should().Be("John Doe");
        subject.ObjectClass.Should().Be("UKSF_B_Medic");
        subject.Account.Should().Be(account);
        subject.Rank.Should().Be(rank);
        subject.Unit.Should().Be(unit);
    }

    [Fact]
    public void ShouldAllowNullAssignments()
    {
        // Arrange
        var subject = new MissionPlayer
        {
            Name = "Test",
            Account = new DomainAccount(),
            Rank = new DomainRank(),
            Unit = new MissionUnit()
        };

        // Act
        subject.Name = null;
        subject.Account = null;
        subject.Rank = null;
        subject.Unit = null;

        // Assert
        subject.Name.Should().BeNull();
        subject.Account.Should().BeNull();
        subject.Rank.Should().BeNull();
        subject.Unit.Should().BeNull();
    }
}
