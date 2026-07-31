using FluentAssertions;
using UKSF.Api.ArmaServer.Npc.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc;

public class NpcNameMatcherTests
{
    private static readonly string[] TwoGuards = ["Merl", "Tomas"];

    [Theory]
    [InlineData("Merl, open the gate.")]
    [InlineData("merl open up")]
    [InlineData("hey Merl")]
    public void Exact_Name_Is_This(string text)
    {
        NpcNameMatcher.Classify(text, "Merl", TwoGuards).Should().Be(NpcNameMatcher.Match.This);
    }

    [Theory]
    [InlineData("Mel, open the gate.")] // STT heard the guard's name short
    [InlineData("Murl, let me through")] // accent flattens the vowel
    [InlineData("Merle, a word")]
    public void Stt_Mangled_Name_Still_Resolves(string text)
    {
        NpcNameMatcher.Classify(text, "Merl", TwoGuards).Should().Be(NpcNameMatcher.Match.This);
    }

    [Theory]
    [InlineData("Tomas, over here.")]
    [InlineData("Thomas, a question")] // the other guard, through a common spelling
    public void Another_Npc_Name_Is_Other(string text)
    {
        NpcNameMatcher.Classify(text, "Merl", TwoGuards).Should().Be(NpcNameMatcher.Match.Other);
    }

    [Theory]
    [InlineData("open the gate please")]
    [InlineData("can I come through?")]
    [InlineData("")]
    public void No_Name_Is_None_So_The_Gaze_Gate_Decides(string text)
    {
        NpcNameMatcher.Classify(text, "Merl", TwoGuards).Should().Be(NpcNameMatcher.Match.None);
    }

    [Fact]
    public void Two_Equally_Plausible_Matches_Are_Borderline_Not_A_Guess()
    {
        // Merl and Marl stand together; "Marl" is one edit from both.
        NpcNameMatcher.Classify("Marl, open up", "Merl", ["Merl", "Marl"]).Should().Be(NpcNameMatcher.Match.Borderline);
    }

    [Theory]
    [InlineData("Parval is your family safe?")] // the exact STT slip that failed in testing
    [InlineData("Parvel, over here")]
    public void A_Two_Edit_Accent_Slip_Still_Resolves_To_The_Only_Plausible_Name(string text)
    {
        NpcNameMatcher.Classify(text, "Pavel", TwoGuardsPavel).Should().Be(NpcNameMatcher.Match.This);
        NpcNameMatcher.Classify(text, "Tomas", TwoGuardsPavel).Should().Be(NpcNameMatcher.Match.Other);
    }

    private static readonly string[] TwoGuardsPavel = ["Tomas", "Pavel"];

    [Fact]
    public void An_Unrelated_Word_Does_Not_Trip_The_Matcher()
    {
        NpcNameMatcher.Classify("the password is swordfish", "Merl", TwoGuards).Should().Be(NpcNameMatcher.Match.None);
    }

    [Fact]
    public void Matching_Is_Cheap_Enough_To_Run_Per_Turn()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < 10000; i++)
        {
            NpcNameMatcher.Classify("Murl, I already told you the password, let me through", "Merl", TwoGuards);
        }

        sw.Stop();
        sw.ElapsedMilliseconds.Should().BeLessThan(500); // ~50µs per turn at worst
    }
}
