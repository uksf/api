using FluentAssertions;
using UKSF.Api.ArmaServer.Npc.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc;

public class NpcPlayerRosterTests
{
    [Fact]
    public void New_Speakers_Get_Unique_Labels_In_Order()
    {
        var session = System.Guid.NewGuid().ToString();

        NpcPlayerRoster.DisplayName(session, "uid-a").Should().Be("Soldier 1");
        NpcPlayerRoster.DisplayName(session, "uid-b").Should().Be("Soldier 2");
        NpcPlayerRoster.DisplayName(session, "uid-a").Should().Be("Soldier 1"); // stable
    }

    [Fact]
    public void Labels_Do_Not_Leak_Between_Sessions()
    {
        NpcPlayerRoster.DisplayName(System.Guid.NewGuid().ToString(), "uid-a")
                       .Should()
                       .Be(NpcPlayerRoster.DisplayName(System.Guid.NewGuid().ToString(), "uid-a"));
    }

    [Theory]
    [InlineData("I'm Beswick", "Beswick")]
    [InlineData("my name's Kowalski", "Kowalski")]
    [InlineData("call me Dutch", "Dutch")]
    [InlineData("I am merl.", "Merl")]
    public void An_Introduction_Upgrades_The_Label(string text, string expected)
    {
        var session = System.Guid.NewGuid().ToString();
        NpcPlayerRoster.DisplayName(session, "uid-a");

        var learned = NpcPlayerRoster.LearnName(session, "uid-a", text);

        learned.Should().NotBeNull();
        learned.Value.OldDisplay.Should().Be("Soldier 1");
        learned.Value.NewName.Should().Be(expected);
        NpcPlayerRoster.DisplayName(session, "uid-a").Should().Be(expected);
    }

    [Theory]
    [InlineData("I'm looking for the soldiers")] // not an introduction
    [InlineData("I'm not sure about that")]
    [InlineData("where did they go?")]
    public void Ordinary_Speech_Is_Not_An_Introduction(string text)
    {
        var session = System.Guid.NewGuid().ToString();

        NpcPlayerRoster.LearnName(session, "uid-a", text).Should().BeNull();
    }

    [Fact]
    public void Learning_The_Same_Name_Twice_Does_Not_Rewrite()
    {
        var session = System.Guid.NewGuid().ToString();

        NpcPlayerRoster.LearnName(session, "uid-a", "I'm Beswick").Should().NotBeNull();
        NpcPlayerRoster.LearnName(session, "uid-a", "I'm Beswick").Should().BeNull();
    }
}
