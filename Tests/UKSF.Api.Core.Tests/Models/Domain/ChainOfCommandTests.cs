using FluentAssertions;
using UKSF.Api.Core.Models.Domain;
using Xunit;

namespace UKSF.Api.Core.Tests.Models.Domain;

public class ChainOfCommandTests
{
    private const string MemberId1 = "member1";
    private const string MemberId2 = "member2";
    private const string MemberId3 = "member3";
    private const string MemberId4 = "member4";

    [Fact]
    public void HasMember_Should_Return_True_When_Member_In_Any_Position()
    {
        // Arrange
        var chainOfCommand = new ChainOfCommand
        {
            OneIC = MemberId1,
            TwoIC = MemberId2,
            ThreeIC = MemberId3,
            NCOIC = MemberId4
        };

        // Act & Assert
        chainOfCommand.HasMember(MemberId1).Should().BeTrue();
        chainOfCommand.HasMember(MemberId2).Should().BeTrue();
        chainOfCommand.HasMember(MemberId3).Should().BeTrue();
        chainOfCommand.HasMember(MemberId4).Should().BeTrue();
        chainOfCommand.HasMember("nonexistent").Should().BeFalse();
    }

    [Fact]
    public void RemoveMember_Should_Remove_Member_From_All_Positions()
    {
        // Arrange
        var chainOfCommand = new ChainOfCommand
        {
            OneIC = MemberId1,
            TwoIC = MemberId1, // Same member in multiple positions
            ThreeIC = MemberId2,
            NCOIC = MemberId3
        };

        // Act
        chainOfCommand.RemoveMember(MemberId1);

        // Assert
        chainOfCommand.OneIC.Should().BeNull();
        chainOfCommand.TwoIC.Should().BeNull();
        chainOfCommand.ThreeIC.Should().Be(MemberId2);
        chainOfCommand.NCOIC.Should().Be(MemberId3);
        chainOfCommand.HasMember(MemberId1).Should().BeFalse();
    }

    [Theory]
    [InlineData("1iC", MemberId1)]
    [InlineData("2iC", MemberId2)]
    [InlineData("3iC", MemberId3)]
    [InlineData("NCOiC", MemberId4)]
    public void GetMemberAtPosition_Should_Return_Correct_Member(string position, string expectedMemberId)
    {
        // Arrange
        var chainOfCommand = new ChainOfCommand
        {
            OneIC = MemberId1,
            TwoIC = MemberId2,
            ThreeIC = MemberId3,
            NCOIC = MemberId4
        };

        // Act
        var result = chainOfCommand.GetMemberAtPosition(position);

        // Assert
        result.Should().Be(expectedMemberId);
    }

    [Fact]
    public void GetMemberAtPosition_Should_Return_Null_For_Invalid_Position()
    {
        // Arrange
        var chainOfCommand = new ChainOfCommand();

        // Act
        var result = chainOfCommand.GetMemberAtPosition("InvalidPosition");

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("1iC")]
    [InlineData("2iC")]
    [InlineData("3iC")]
    [InlineData("NCOiC")]
    public void SetMemberAtPosition_Should_Set_Member_Correctly(string position)
    {
        // Arrange
        var chainOfCommand = new ChainOfCommand();

        // Act
        chainOfCommand.SetMemberAtPosition(position, MemberId1);

        // Assert
        chainOfCommand.GetMemberAtPosition(position).Should().Be(MemberId1);
    }

    [Fact]
    public void SetMemberAtPosition_Should_Do_Nothing_For_Invalid_Position()
    {
        // Arrange
        var chainOfCommand = new ChainOfCommand();

        // Act
        chainOfCommand.SetMemberAtPosition("InvalidPosition", MemberId1);

        // Assert
        chainOfCommand.OneIC.Should().BeNull();
        chainOfCommand.TwoIC.Should().BeNull();
        chainOfCommand.ThreeIC.Should().BeNull();
        chainOfCommand.NCOIC.Should().BeNull();
    }

    [Theory]
    [InlineData("1iC", true)]
    [InlineData("2iC", true)]
    [InlineData("3iC", false)]
    [InlineData("NCOiC", false)]
    public void HasPosition_Should_Return_Correct_Status(string position, bool expected)
    {
        // Arrange
        var chainOfCommand = new ChainOfCommand
        {
            OneIC = MemberId1, TwoIC = MemberId2
            // ThreeIC and NCOIC are null
        };

        // Act
        var result = chainOfCommand.HasPosition(position);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("1iC", 0)]
    [InlineData("2iC", 1)]
    [InlineData("3iC", 2)]
    [InlineData("NCOiC", 3)]
    [InlineData("InvalidPosition", int.MaxValue)]
    public void GetPositionOrder_Should_Return_Correct_Order(string position, int expectedOrder)
    {
        // Arrange
        var chainOfCommand = new ChainOfCommand();

        // Act
        var result = chainOfCommand.GetPositionOrder(position);

        // Assert
        result.Should().Be(expectedOrder);
    }

    [Fact]
    public void GetAssignedPositions_Should_Return_Only_Assigned_Positions()
    {
        // Arrange
        var chainOfCommand = new ChainOfCommand
        {
            OneIC = MemberId1, ThreeIC = MemberId3
            // TwoIC and NCOIC are null
        };

        // Act
        var result = chainOfCommand.GetAssignedPositions();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(("1iC", MemberId1));
        result.Should().Contain(("3iC", MemberId3));
        result.Should().NotContain(x => x.Position == "2iC");
        result.Should().NotContain(x => x.Position == "NCOiC");
    }

    [Fact]
    public void GetAssignedPositions_Should_Return_Empty_When_No_Positions_Assigned()
    {
        // Arrange
        var chainOfCommand = new ChainOfCommand();

        // Act
        var result = chainOfCommand.GetAssignedPositions();

        // Assert
        result.Should().BeEmpty();
    }
}
