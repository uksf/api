using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.ArmaServer.Npc.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc;

/// Opt-in real-model corpus. Normal CI never hits the network.
/// Enable: NPC_GUARDED_LIVE_FIXTURES=1 and CLACKS_URL=<url>.
public class NpcGuardedLiveFixtureTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private const int Repetitions = 5;

    private static bool LiveEnabled => string.Equals(Environment.GetEnvironmentVariable("NPC_GUARDED_LIVE_FIXTURES"), "1", StringComparison.Ordinal);

    [Fact]
    public async Task LiveCorpus_RunsAgainstClacks_WhenEnabled()
    {
        if (!LiveEnabled)
        {
            // Permanent skip path for normal test runs — no production service calls.
            return;
        }

        var clacksUrl = Environment.GetEnvironmentVariable("CLACKS_URL")?.TrimEnd('/');
        clacksUrl.Should().NotBeNullOrWhiteSpace("CLACKS_URL required when NPC_GUARDED_LIVE_FIXTURES=1");

        var corpus = LoadCorpus();
        corpus.Cases.Should().HaveCount(8);
        var brain = BuildLiveBrain(clacksUrl!);

        foreach (var cse in corpus.Cases)
        {
            for (var rep = 0; rep < Repetitions; rep++)
            {
                await RunCaseAsync(brain, corpus, cse, rep);
            }
        }
    }

    private static async Task RunCaseAsync(NpcBrainService brain, CorpusFile corpus, CorpusCase cse, int rep)
    {
        var state = BuildPriorState(cse);
        var utterances = cse.Utterances.Select((text, i) => new NpcTurnDto
                                {
                                    SpeakerId = "p1",
                                    SpeakerName = "Player",
                                    Text = text,
                                    T = 1_700_000_000_000L + i
                                }
                            )
                            .ToList();

        var turn = await TurnOnce(brain, corpus, state, utterances);
        if (turn?.Classify is null && turn?.Reply?.Failure == "null model")
        {
            turn = await TurnOnce(brain, corpus, state, utterances);
        }

        var classify = turn?.Classify;
        classify.Should().NotBeNull($"case {cse.Id} rep {rep}: classify null/rejected ({turn?.Reply?.Failure})");
        classify!.Classifications.Should().HaveCount(utterances.Count, cse.Id);
        Console.WriteLine(
            $"{cse.Id} rep {rep}: {classify.Ms}ms tags=[{string.Join(",", classify.Classifications.Select(c => c.Tag))}] replyOk={turn.Reply?.Ok}"
        );

        if (cse.ExpectTags is { Count: > 0 }) classify.Classifications.Select(c => c.Tag).Should().Equal(cse.ExpectTags, cse.Id);
        if (cse.ExpectTopicSlots is { Count: > 0 })
            classify.Classifications.Select(c => c.TopicSlot).Should().Equal(cse.ExpectTopicSlots.Select(s => (int?)s), cse.Id);
        if (cse.ExpectAddressesConcern is { Count: > 0 })
            classify.Classifications.Select(c => c.AddressesConcern).Should().Equal(cse.ExpectAddressesConcern, cse.Id);
        if (cse.ExpectAmbiguous is { Count: > 0 }) classify.Classifications.Select(c => c.Ambiguous).Should().Equal(cse.ExpectAmbiguous, cse.Id);

        var config = new NpcGuardedConfig
        {
            Concern = corpus.Concern,
            Facts = corpus.Facts.Select(f => new NpcGuardedFact
                              {
                                  Id = f.Id,
                                  Topic = f.Topic,
                                  Text = f.Text
                              }
                          )
                          .ToList()
        };
        var engine = NpcGuardedProfile.Evaluate(state, config, classify.Classifications);

        if (cse.ExpectPermittedFactId is not null || cse.ExpectPermittedFactIdSpecified) engine.PermittedFactId.Should().Be(cse.ExpectPermittedFactId, cse.Id);
        if (!string.IsNullOrEmpty(cse.ExpectBand)) engine.NextState.CooperationBand.Should().Be(cse.ExpectBand, cse.Id);
        if (!string.IsNullOrEmpty(cse.ExpectDirective)) engine.Directive.Should().Be(cse.ExpectDirective, cse.Id);
        if (cse.ExpectWarning is not null) engine.NextState.PendingWarning.Should().Be(cse.ExpectWarning.Value, cse.Id);

        var reply = turn.Reply;
        if (reply is not { Ok: true })
        {
            return;
        }

        var validated = NpcGuardedReplyValidator.Validate(
            new NpcGuardedReplyModelOutput
            {
                Text = reply.Text,
                Mood = reply.Mood,
                Emote = reply.Emote,
                DisclosedFactId = reply.DisclosedFactId
            },
            config,
            engine.PermittedFactId,
            engine.PermittedFactText,
            state.DisclosedFactIds
        );

        if (!validated.Ok) return;

        MoodScripts.IsValid(validated.Mood).Should().BeTrue(cse.Id);
        if (validated.Emote is not null) validated.Emote.Length.Should().BeLessThanOrEqualTo(NpcGuardedReplyValidator.MaxEmoteLength, cse.Id);

        if (!string.IsNullOrEmpty(validated.DisclosedFactId))
        {
            validated.DisclosedFactId.Should().Be(engine.PermittedFactId, cse.Id);
            var fact = config.Facts.Single(f => f.Id == validated.DisclosedFactId);
            validated.SpokenText.Should().Contain(fact.Text, cse.Id);
        }

        foreach (var fact in config.Facts.Where(f => !state.DisclosedFactIds.Contains(f.Id) && f.Id != validated.DisclosedFactId))
        {
            // Model dialogue alone must not introduce other undisclosed canonical sentences.
            if (string.IsNullOrEmpty(validated.DisclosedFactId) || fact.Id != validated.DisclosedFactId)
                reply.Text.Should().NotContain(fact.Text, because: $"{cse.Id} must not author undisclosed {fact.Id}");
        }
    }

    private static NpcGuardedState BuildPriorState(CorpusCase cse)
    {
        var state = new NpcGuardedState();
        if (cse.PriorState is null) return state;
        if (!string.IsNullOrEmpty(cse.PriorState.CooperationBand)) state.CooperationBand = cse.PriorState.CooperationBand;
        state.PendingWarning = cse.PriorState.PendingWarning;
        state.Burned = cse.PriorState.Burned;
        if (cse.PriorState.DisclosedFactIds is { Count: > 0 }) state.DisclosedFactIds = [..cse.PriorState.DisclosedFactIds];
        return state;
    }

    private static Task<NpcGuardedTurnResult> TurnOnce(NpcBrainService brain, CorpusFile corpus, NpcGuardedState state, List<NpcTurnDto> utterances) =>
        brain.TurnGuardedAsync(
            new NpcGuardedTurnRequest
            {
                NpcId = "live-tomas",
                Persona = new NpcPersona
                {
                    Name = "Tomas",
                    Role = "farmer",
                    Language = "English",
                    Mood = "wary",
                    AttitudeToPlayers = "cautious"
                },
                Knowledge = "local farmer brief",
                Concern = corpus.Concern,
                TopicCues = corpus.Facts.Select(f => (f.Id, f.Topic)).ToList(),
                State = state.Clone(),
                History = [],
                NewTurns = utterances,
                VoiceId = "bm_george"
            }
        );

    private static NpcBrainService BuildLiveBrain(string clacksUrl)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient());
        var vars = new Mock<IVariablesService>();
        vars.Setup(x => x.GetVariable("CLACKS_URL")).Returns(new DomainVariableItem { Key = "CLACKS_URL", Item = clacksUrl });
        var voices = new Mock<INpcVoicesContext>();
        voices.Setup(x => x.GetSingle(It.IsAny<Func<DomainNpcVoice, bool>>())).Returns((DomainNpcVoice)null);
        var clacks = new ClacksClient(factory.Object, vars.Object, Mock.Of<IUksfLogger>());
        return new NpcBrainService(clacks, voices.Object, Mock.Of<IUksfLogger>());
    }

    private static CorpusFile LoadCorpus()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Npc", "TestData", "guarded-source-real-model-corpus.json");
        return JsonSerializer.Deserialize<CorpusFile>(File.ReadAllText(path), JsonOpts)!;
    }

    private sealed class CorpusFile
    {
        public string Concern { get; set; }
        public List<FactDto> Facts { get; set; }
        public List<CorpusCase> Cases { get; set; }
    }

    private sealed class FactDto
    {
        public string Id { get; set; }
        public string Topic { get; set; }
        public string Text { get; set; }
    }

    private sealed class CorpusCase
    {
        public string Id { get; set; }
        public List<string> Utterances { get; set; }
        public List<string> ExpectTags { get; set; }
        public List<int> ExpectTopicSlots { get; set; }
        public List<bool> ExpectAddressesConcern { get; set; }
        public List<bool> ExpectAmbiguous { get; set; }
        public string ExpectPermittedFactId { get; set; }
        public bool ExpectPermittedFactIdSpecified => true; // always assert (null is meaningful)
        public string ExpectBand { get; set; }
        public string ExpectDirective { get; set; }
        public bool? ExpectWarning { get; set; }
        public PriorStateDto PriorState { get; set; }
    }

    private sealed class PriorStateDto
    {
        public string CooperationBand { get; set; }
        public bool PendingWarning { get; set; }
        public bool Burned { get; set; }
        public List<string> DisclosedFactIds { get; set; }
    }
}
