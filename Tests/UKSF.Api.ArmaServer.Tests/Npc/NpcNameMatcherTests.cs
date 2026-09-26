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

    [Fact]
    public void A_Two_Edit_Slip_Is_Borderline_So_Gaze_Owns_The_Turn()
    {
        NpcNameMatcher.Classify("Parval is your family safe?", "Pavel", TwoGuardsPavel).Should().Be(NpcNameMatcher.Match.Borderline);
        NpcNameMatcher.Classify("Parval is your family safe?", "Tomas", TwoGuardsPavel).Should().NotBe(NpcNameMatcher.Match.This);
    }

    [Fact]
    public void A_One_Edit_Name_Is_Still_This()
    {
        NpcNameMatcher.Classify("Parvel, over here", "Pavel", TwoGuardsPavel).Should().Be(NpcNameMatcher.Match.This);
    }

    private static readonly string[] TwoGuardsPavel = ["Tomas", "Pavel"];

    [Fact]
    public void An_Unrelated_Word_Does_Not_Trip_The_Matcher()
    {
        NpcNameMatcher.Classify("the password is swordfish", "Merl", TwoGuards).Should().Be(NpcNameMatcher.Match.None);
    }

    [Theory]
    [InlineData("what do people around here think")]
    [InlineData("available now or later")]
    public void A_Common_Word_That_Sounds_Like_Pavel_Is_Not_A_Name(string text)
    {
        NpcNameMatcher.Classify(text, "Pavel", TwoGuardsPavel).Should().NotBe(NpcNameMatcher.Match.This);
        NpcNameMatcher.Classify(text, "Tomas", TwoGuardsPavel).Should().NotBe(NpcNameMatcher.Match.This);
    }

    [Fact]
    public void Gaze_Target_Answers_Unless_Another_Npc_Is_Named()
    {
        NpcBrokerService.DecideAddress(NpcNameMatcher.Match.None, true).Should().Be(NpcBrokerService.AddressDecision.Answer);
        NpcBrokerService.DecideAddress(NpcNameMatcher.Match.Borderline, true).Should().Be(NpcBrokerService.AddressDecision.Answer);
        NpcBrokerService.DecideAddress(NpcNameMatcher.Match.Other, true).Should().Be(NpcBrokerService.AddressDecision.StaySilent);
    }

    [Fact]
    public void Non_Gaze_Npc_Answers_Only_On_A_Solid_Name()
    {
        NpcBrokerService.DecideAddress(NpcNameMatcher.Match.This, false).Should().Be(NpcBrokerService.AddressDecision.Answer);
        NpcBrokerService.DecideAddress(NpcNameMatcher.Match.Borderline, false).Should().Be(NpcBrokerService.AddressDecision.StaySilent);
        NpcBrokerService.DecideAddress(NpcNameMatcher.Match.None, false).Should().Be(NpcBrokerService.AddressDecision.StaySilent);
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

        // One call per turn is the real budget; 10k iterations only make it measurable.
        // Phonetic fallback made each call dearer, and a tenth of a millisecond against a
        // turn that spends over a second in the brain is still free.
        var perCall = sw.Elapsed.TotalMilliseconds / 10000;
        perCall.Should().BeLessThan(0.5);
    }
}
