using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.ArmaServer.Npc.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc;

public class NpcGuardedProfileTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly NpcGuardedConfig _config;
    private readonly FixtureFile _fixtures;

    public NpcGuardedProfileTests()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Npc", "TestData", "guarded-source-fixtures.json");
        _fixtures = JsonSerializer.Deserialize<FixtureFile>(File.ReadAllText(path), JsonOpts)!;
        _config = new NpcGuardedConfig
        {
            Concern = _fixtures.Concern,
            Facts = _fixtures.Facts.Select(f => new NpcGuardedFact
                                 {
                                     Id = f.Id,
                                     Topic = f.Topic,
                                     Text = f.Text
                                 }
                             )
                             .ToList()
        };
    }

    [Fact]
    public void AllFixturePaths_AreDeterministic_AcrossFortyRuns()
    {
        for (var rep = 0; rep < 5; rep++)
        {
            foreach (var path in _fixtures.Paths) RunPath(path);
        }
    }

    [Theory]
    [InlineData("clean-three-step")]
    [InlineData("direct-fact-3")]
    [InlineData("two-relevant-one-fact")]
    [InlineData("pressure-recover")]
    [InlineData("threat-backoff")]
    [InlineData("threat-burn")]
    [InlineData("threat-backoff-order-in-batch")]
    [InlineData("threat-then-question-in-batch")]
    [InlineData("question-then-threat-in-batch")]
    [InlineData("ambiguous-no-change")]
    public void FixturePath_MatchesExpectedState(string pathId)
    {
        var path = _fixtures.Paths.Single(p => p.Id == pathId);
        RunPath(path);
    }

    [Fact]
    public void RelevantQuestion_WithAddressesConcern_PermitsFact3()
    {
        var state = new NpcGuardedState { CooperationBand = NpcCooperationBands.Cooperative, DisclosedFactIds = ["f1", "f2"] };
        var result = NpcGuardedProfile.Evaluate(
            state,
            _config,
            [
                new NpcGuardedClassification
                {
                    T = 1,
                    Tag = NpcGuardedTags.RelevantQuestion,
                    TopicSlot = 3,
                    AddressesConcern = true,
                    Ambiguous = false,
                    Evidence = "when"
                }
            ]
        );
        result.PermittedFactId.Should().Be("f3");
        result.Directive.Should().Be(NpcGuardedDirectives.Disclose);
    }

    [Fact]
    public void AddressesConcernTagAlone_DoesNotPermitFactWithoutTopic()
    {
        var state = new NpcGuardedState { CooperationBand = NpcCooperationBands.Cooperative, DisclosedFactIds = ["f1", "f2"] };
        var result = NpcGuardedProfile.Evaluate(
            state,
            _config,
            [
                new NpcGuardedClassification
                {
                    T = 1,
                    Tag = NpcGuardedTags.AddressesConcern,
                    AddressesConcern = true,
                    Ambiguous = false,
                    Evidence = "safe"
                }
            ]
        );
        result.PermittedFactId.Should().BeNull();
    }

    [Fact]
    public void UnknownTag_IsIgnored()
    {
        var result = NpcGuardedProfile.Evaluate(
            new NpcGuardedState(),
            _config,
            [
                new NpcGuardedClassification
                {
                    T = 1,
                    Tag = "not_a_real_tag",
                    Ambiguous = false
                }
            ]
        );
        result.NextState.CooperationBand.Should().Be(NpcCooperationBands.Guarded);
        result.PermittedFactId.Should().BeNull();
        result.Directive.Should().Be(NpcGuardedDirectives.Safe);
    }

    [Fact]
    public void EmptyClassifications_SafeNoChange()
    {
        var result = NpcGuardedProfile.Evaluate(new NpcGuardedState(), _config, []);
        result.NextState.DisclosedFactIds.Should().BeEmpty();
        result.Directive.Should().Be(NpcGuardedDirectives.Safe);
    }

    private void RunPath(FixturePath path)
    {
        var state = new NpcGuardedState();
        foreach (var step in path.Steps)
        {
            var classifications = step.Classifications.Select(c => new NpcGuardedClassification
                                          {
                                              T = c.T,
                                              Tag = c.Tag,
                                              TopicSlot = c.TopicSlot,
                                              AddressesConcern = c.AddressesConcern,
                                              Ambiguous = c.Ambiguous,
                                              Reason = c.Reason,
                                              Evidence = c.Evidence
                                          }
                                      )
                                      .ToList();
            var result = NpcGuardedProfile.Evaluate(state, _config, classifications);
            result.NextState.CooperationBand.Should().Be(step.ExpectBand, path.Id);
            if (step.ExpectWarning is not null) result.NextState.PendingWarning.Should().Be(step.ExpectWarning.Value, path.Id);
            if (step.ExpectBurned is not null) result.NextState.Burned.Should().Be(step.ExpectBurned.Value, path.Id);
            result.Directive.Should().Be(step.ExpectDirective, path.Id);
            result.PermittedFactId.Should().Be(step.ExpectPermitted, path.Id);

            state = result.NextState.Clone();
            if (!string.IsNullOrEmpty(result.PermittedFactId) && !state.DisclosedFactIds.Contains(result.PermittedFactId))
                state.DisclosedFactIds.Add(result.PermittedFactId);

            state.DisclosedFactIds.Should().Equal(step.ExpectDisclosed ?? [], path.Id);
        }
    }

    private sealed class FixtureFile
    {
        public string Concern { get; set; }
        public List<FactDto> Facts { get; set; }
        public List<FixturePath> Paths { get; set; }
    }

    private sealed class FactDto
    {
        public string Id { get; set; }
        public string Topic { get; set; }
        public string Text { get; set; }
    }

    private sealed class FixturePath
    {
        public string Id { get; set; }
        public List<FixtureStep> Steps { get; set; }
    }

    private sealed class FixtureStep
    {
        public List<ClassDto> Classifications { get; set; }
        public string ExpectBand { get; set; }
        public bool? ExpectWarning { get; set; }
        public bool? ExpectBurned { get; set; }
        public List<string> ExpectDisclosed { get; set; }
        public string ExpectDirective { get; set; }
        public string ExpectPermitted { get; set; }
    }

    private sealed class ClassDto
    {
        public long T { get; set; }
        public string Tag { get; set; }
        public int? TopicSlot { get; set; }
        public bool AddressesConcern { get; set; }
        public bool Ambiguous { get; set; }
        public string Reason { get; set; }
        public string Evidence { get; set; }
    }
}
