using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.ArmaServer.Npc.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc;

/// Opt-in bench: the current one-call guarded turn against Jev-decides / Gemini-writes, over the
/// same hand-labelled cases. Reports, never asserts. Normal CI never hits the network.
/// Enable: NPC_JEV_BENCH=1, CLACKS_URL=<url>, optional NPC_JEV_BENCH_REPS and NPC_JEV_BENCH_OUT=<file>.
public class NpcGuardedJevBenchTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task Bench_CurrentFlowAgainstJevFlow_WhenEnabled()
    {
        if (Environment.GetEnvironmentVariable("NPC_JEV_BENCH") != "1") return;

        var clacksUrl = Environment.GetEnvironmentVariable("CLACKS_URL")!.TrimEnd('/');
        var reps = int.TryParse(Environment.GetEnvironmentVariable("NPC_JEV_BENCH_REPS"), out var r) ? r : 3;
        var (brain, jevBrain) = Build(clacksUrl);
        var rows = new List<Row>();

        foreach (var file in new[] { "guarded-source-real-model-corpus.json", "guarded-source-hard-cases.json" })
        {
            var corpus = JsonSerializer.Deserialize<Corpus>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Npc", "TestData", file)), JsonOpts)!;
            var config = new NpcGuardedConfig
            {
                Concern = corpus.Concern,
                Facts = corpus.Facts.Select(f => new NpcGuardedFact { Id = f.Id, Topic = f.Topic, Text = f.Text }).ToList()
            };
            // Warm both paths so a cold model load does not land on the first case.
            await brain.TurnGuardedAsync(Request(corpus.Cases[0], config));
            await jevBrain.TurnAsync(Request(corpus.Cases[0], config), config);

            foreach (var cse in corpus.Cases)
            {
                for (var rep = 0; rep < reps; rep++)
                {
                    var req = Request(cse, config);
                    var watch = Stopwatch.StartNew();
                    var current = await brain.TurnGuardedAsync(req);
                    var currentMs = watch.ElapsedMilliseconds;
                    var currentTags = current?.Classify?.Classifications;
                    rows.Add(Score("current", cse, config, req, currentTags, currentMs, current?.Reply?.Text, current?.Reply?.Mood));

                    req = Request(cse, config);
                    watch.Restart();
                    var viaJev = await jevBrain.TurnAsync(req, config);
                    rows.Add(
                        Score("jev", cse, config, req, viaJev.Classifications, watch.ElapsedMilliseconds, viaJev.Text ?? $"[{viaJev.Failure}]", viaJev.Mood) with
                        {
                            DecideMs = viaJev.DecideMs, WriteMs = viaJev.WriteMs
                        }
                    );
                }
            }
        }

        var report = Report(rows);
        Console.WriteLine(report);
        var outPath = Environment.GetEnvironmentVariable("NPC_JEV_BENCH_OUT");
        if (!string.IsNullOrEmpty(outPath)) await File.WriteAllTextAsync(outPath, report);
    }

    private static Row Score(string flow, Case cse, NpcGuardedConfig config, NpcGuardedTurnRequest req, List<NpcGuardedClassification> tags, long ms, string text, string mood)
    {
        var misses = new List<string>();
        if (tags is null || tags.Count != cse.Utterances.Count) misses.Add("no classifications");
        else
        {
            void Check<T>(string name, List<T> want, IEnumerable<T> got)
            {
                var g = got.ToList();
                if (want is { Count: > 0 } && !want.SequenceEqual(g)) misses.Add($"{name} want [{string.Join(",", want)}] got [{string.Join(",", g)}]");
            }

            Check("tag", cse.ExpectTags, tags.Select(t => t.Tag));
            Check("slot", cse.ExpectTopicSlots, tags.Select(t => t.TopicSlot));
            Check("concern", cse.ExpectAddressesConcern, tags.Select(t => t.AddressesConcern));
            Check("ambiguous", cse.ExpectAmbiguous, tags.Select(t => t.Ambiguous));
            if (cse.ExpectNotTags is { Count: > 0 } && tags.Any(t => cse.ExpectNotTags.Contains(t.Tag))) misses.Add($"tag must not be {string.Join(",", cse.ExpectNotTags)}");
            var engine = NpcGuardedProfile.Evaluate(req.State, config, tags);
            if (engine.PermittedFactId != cse.ExpectPermittedFactId) misses.Add($"permitted want {cse.ExpectPermittedFactId ?? "none"} got {engine.PermittedFactId ?? "none"}");
            if (cse.ExpectWarning is { } warn && engine.NextState.PendingWarning != warn) misses.Add($"warning want {warn}");
        }

        return new Row(flow, cse.Id, ms, misses, text, mood);
    }

    private static string Report(List<Row> rows)
    {
        var sb = new StringBuilder();
        foreach (var flow in new[] { "current", "jev" })
        {
            var mine = rows.Where(x => x.Flow == flow).ToList();
            var ms = mine.Select(x => x.Ms).OrderBy(x => x).ToList();
            sb.AppendLine(
                $"{flow}: decisions right {mine.Count(x => x.Misses.Count == 0)}/{mine.Count}, p50 {ms[ms.Count / 2]}ms, p90 {ms[(int)(ms.Count * 0.9)]}ms, max {ms[^1]}ms"
            );
            if (flow == "jev")
            {
                var d = mine.Select(x => x.DecideMs).OrderBy(x => x).ToList();
                var w = mine.Select(x => x.WriteMs).OrderBy(x => x).ToList();
                sb.AppendLine($"  jev decide p50 {d[d.Count / 2]}ms, writer p50 {w[w.Count / 2]}ms");
            }

            sb.AppendLine("  moods " + string.Join(", ", mine.GroupBy(x => x.Mood ?? "?").OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {g.Count()}")));
            var facts = new[] { "Trucks have been rolling past the farm after dark", "They stop at the old mill by the river bend", "They come back every third night near midnight" };
            sb.AppendLine($"  fact said word for word {mine.Count(x => facts.Any(f => (x.Text ?? "").Contains(f, StringComparison.OrdinalIgnoreCase)))}");
            foreach (var miss in mine.Where(x => x.Misses.Count > 0)) sb.AppendLine($"  MISS {miss.Case}: {string.Join("; ", miss.Misses)}");
        }

        sb.AppendLine();
        foreach (var group in rows.GroupBy(x => x.Case))
        {
            sb.AppendLine(group.Key);
            foreach (var row in group.GroupBy(x => x.Flow).Select(g => g.First())) sb.AppendLine($"  {row.Flow,-7} {row.Ms,5}ms [{row.Mood}] {row.Text}");
        }

        return sb.ToString();
    }

    private static NpcGuardedTurnRequest Request(Case cse, NpcGuardedConfig config)
    {
        var state = new NpcGuardedState();
        if (cse.PriorState is { } prior)
        {
            if (!string.IsNullOrEmpty(prior.CooperationBand)) state.CooperationBand = prior.CooperationBand;
            state.PendingWarning = prior.PendingWarning;
            state.Burned = prior.Burned;
            state.DisclosedFactIds = [..prior.DisclosedFactIds ?? []];
        }

        // Mirrors NpcBrokerService.Guarded: what the current one-call prompt is shown.
        var next = NpcGuardedProfile.NextIncludableFact(config, state);
        var told = config.Facts.Where(f => state.DisclosedFactIds.Any(id => NpcGuardedFactIds.Same(id, f.Id))).ToList();
        return new NpcGuardedTurnRequest
        {
            NpcId = "bench-tomas",
            Persona = new NpcPersona { Name = "Tomas", Role = "farmer", Language = "English", Mood = "wary", AttitudeToPlayers = "cautious" },
            Knowledge = "A farmer who has worked the land by the river all his life. He keeps his head down.",
            Concern = config.Concern,
            TopicCues = config.Facts.Select(f => (f.Id, f.Topic)).ToList(),
            DisclosedFacts = told,
            NextFact = next,
            LaterTopics = config.Facts.Where(f => (next is null || !NpcGuardedFactIds.Same(f.Id, next.Id)) && !told.Contains(f)).Select(f => (f.Id, f.Topic)).ToList(),
            State = state,
            History = [],
            NewTurns = cse.Utterances.Select((text, i) => new NpcTurnDto { SpeakerId = "p1", SpeakerName = "Player", Text = text, T = 1_700_000_000_000L + i })
                          .ToList(),
            VoiceId = "bm_george"
        };
    }

    private static (NpcBrainService, NpcGuardedJevBrain) Build(string clacksUrl)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient());
        var vars = new Mock<IVariablesService>();
        vars.Setup(x => x.GetVariable("CLACKS_URL")).Returns(new DomainVariableItem { Key = "CLACKS_URL", Item = clacksUrl });
        var voices = new Mock<INpcVoicesContext>();
        voices.Setup(x => x.GetSingle(It.IsAny<Func<DomainNpcVoice, bool>>())).Returns((DomainNpcVoice)null);
        var clacks = new ClacksClient(factory.Object, vars.Object, Mock.Of<IUksfLogger>());
        var jev = new NpcJevClient(factory.Object, vars.Object, Mock.Of<IUksfLogger>());
        return (new NpcBrainService(clacks, voices.Object, Mock.Of<IUksfLogger>()), new NpcGuardedJevBrain(jev, clacks, Mock.Of<IUksfLogger>()));
    }

    private sealed record Row(string Flow, string Case, long Ms, List<string> Misses, string Text, string Mood)
    {
        public long DecideMs { get; init; }
        public long WriteMs { get; init; }
    }

    private sealed class Corpus
    {
        public string Concern { get; set; }
        public List<FactDto> Facts { get; set; }
        public List<Case> Cases { get; set; }
    }

    private sealed class FactDto
    {
        public string Id { get; set; }
        public string Topic { get; set; }
        public string Text { get; set; }
    }

    private sealed class Case
    {
        public string Id { get; set; }
        public List<string> Utterances { get; set; }
        public List<string> ExpectTags { get; set; }
        public List<string> ExpectNotTags { get; set; }
        public List<int?> ExpectTopicSlots { get; set; }
        public List<bool> ExpectAddressesConcern { get; set; }
        public List<bool> ExpectAmbiguous { get; set; }
        public string ExpectPermittedFactId { get; set; }
        public bool? ExpectWarning { get; set; }
        public Prior PriorState { get; set; }
    }

    private sealed class Prior
    {
        public string CooperationBand { get; set; }
        public bool PendingWarning { get; set; }
        public bool Burned { get; set; }
        public List<string> DisclosedFactIds { get; set; }
    }
}
