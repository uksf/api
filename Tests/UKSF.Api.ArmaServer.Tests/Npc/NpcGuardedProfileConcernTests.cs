using FluentAssertions;
using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.ArmaServer.Npc.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc;

public class NpcGuardedProfileConcernTests
{
    private static readonly NpcGuardedConfig Config = new()
    {
        Concern = "If those men find out that you heard it from me, my family will pay for it.",
        Facts =
        [
            new NpcGuardedFact
            {
                Id = "1",
                Topic = "strange vehicles on the track today",
                Text = "A grey pickup came down the north track."
            },
            new NpcGuardedFact
            {
                Id = "2",
                Topic = "where that vehicle stopped",
                Text = "It stopped at the old grain store."
            },
            new NpcGuardedFact
            {
                Id = "3",
                Topic = "when they come back",
                Text = "They come back at dusk."
            }
        ]
    };

    private static NpcGuardedState TwoToldCooperative() => new() { CooperationBand = NpcCooperationBands.Cooperative, DisclosedFactIds = ["1", "2"] };

    private static NpcGuardedClassification Move(string tag, int? slot = null, bool concern = false) =>
        new()
        {
            T = 1,
            Tag = tag,
            TopicSlot = slot,
            AddressesConcern = concern,
            Evidence = "x"
        };

    private static NpcGuardedEngineResult Step(NpcGuardedState state, NpcGuardedClassification move) => NpcGuardedProfile.Evaluate(state, Config, [move]);

    [Fact]
    public void QuestionWithoutReassurance_HoldsFact3ForConcern()
    {
        var result = Step(TwoToldCooperative(), Move(NpcGuardedTags.RelevantQuestion, 3));

        result.Directive.Should().Be(NpcGuardedDirectives.Normal);
        result.PermittedFactId.Should().BeNull();
        result.HeldForConcern.Should().BeTrue();
        result.NextState.AskedFactId.Should().Be("3");
    }

    [Fact]
    public void ReassuranceAfterHeldQuestion_DisclosesFact3()
    {
        var asked = Step(TwoToldCooperative(), Move(NpcGuardedTags.RelevantQuestion, 3)).NextState;

        var result = Step(asked, Move(NpcGuardedTags.AddressesConcern, concern: true));

        result.Directive.Should().Be(NpcGuardedDirectives.Disclose);
        result.PermittedFactId.Should().Be("3");
        result.HeldForConcern.Should().BeFalse();
    }

    [Fact]
    public void ReassuranceBeforeQuestion_IsRemembered()
    {
        var reassured = Step(TwoToldCooperative(), Move(NpcGuardedTags.AddressesConcern, concern: true)).NextState;
        reassured.ConcernAddressed.Should().BeTrue();

        var result = Step(reassured, Move(NpcGuardedTags.RelevantQuestion, 3));

        result.PermittedFactId.Should().Be("3");
    }

    [Fact]
    public void ThreatForgetsReassuranceAndHeldQuestion()
    {
        var state = TwoToldCooperative();
        state.ConcernAddressed = true;
        state.AskedFactId = "3";

        var result = Step(state, Move(NpcGuardedTags.Threat));

        result.NextState.ConcernAddressed.Should().BeFalse();
        result.NextState.AskedFactId.Should().BeNull();
    }

    [Fact]
    public void LiveSession_MisheardThreatThenCorrection_ReleasesFact3OnNextTurn()
    {
        var asked = Step(TwoToldCooperative(), Move(NpcGuardedTags.RelevantQuestion, 3)).NextState;
        var warned = Step(asked, Move(NpcGuardedTags.Threat, concern: true)).NextState;
        var backedOff = Step(warned, Move(NpcGuardedTags.BackOff, concern: true));

        backedOff.Directive.Should().Be(NpcGuardedDirectives.BackOff);
        backedOff.NextState.ConcernAddressed.Should().BeTrue();

        var result = Step(backedOff.NextState, Move(NpcGuardedTags.RelevantQuestion, 3));

        result.Directive.Should().Be(NpcGuardedDirectives.Disclose);
        result.PermittedFactId.Should().Be("3");
    }

    [Fact]
    public void Writer_HeldForConcern_IsToldToVoiceTheFear()
    {
        var engine = Step(TwoToldCooperative(), Move(NpcGuardedTags.RelevantQuestion, 3));
        var req = new NpcGuardedTurnRequest { Concern = Config.Concern };

        var system = NpcGuardedJevBrain.BuildWriterSystem(req, engine, "afraid");

        system.Should().Contain("Tell them plainly what you are afraid of").And.Contain(Config.Concern).And.NotContain("They come back at dusk");
    }

    [Fact]
    public void Writer_AfterReassurance_DropsTheFearLine()
    {
        var state = TwoToldCooperative();
        state.ConcernAddressed = true;
        var engine = Step(state, Move(NpcGuardedTags.Rapport));

        var system = NpcGuardedJevBrain.BuildWriterSystem(new NpcGuardedTurnRequest { Concern = Config.Concern }, engine, "neutral");

        system.Should().NotContain(Config.Concern);
    }

    [Fact]
    public void EarlierFacts_NeverWaitForConcern()
    {
        var result = Step(new NpcGuardedState(), Move(NpcGuardedTags.RelevantQuestion, 1));

        result.PermittedFactId.Should().Be("1");
        result.HeldForConcern.Should().BeFalse();
    }
}
