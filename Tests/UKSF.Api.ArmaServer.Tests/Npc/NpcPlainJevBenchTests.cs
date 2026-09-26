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

/// Opt-in bench for plain NPCs: the current flow against Jev, over plain-npc-cases.json. Reports,
/// never asserts. Enable: NPC_JEV_BENCH=1, CLACKS_URL, optional NPC_JEV_BENCH_REPS, NPC_JEV_BENCH_OUT.
public class NpcPlainJevBenchTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task Bench_PlainNpcs_WhenEnabled()
    {
        if (Environment.GetEnvironmentVariable("NPC_JEV_BENCH") != "1") return;

        var url = Environment.GetEnvironmentVariable("CLACKS_URL")!.TrimEnd('/');
        var reps = int.TryParse(Environment.GetEnvironmentVariable("NPC_JEV_BENCH_REPS"), out var r) ? r : 3;
        var data = JsonSerializer.Deserialize<Cases>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Npc", "TestData", "plain-npc-cases.json")), JsonOpts)!;
        var (brain, jevBrain, jev) = Build(url);
        var sb = new StringBuilder();
        var tally = new Dictionary<string, (int Right, int Total, List<long> Ms)>();

        void Score(string key, bool right, long ms, string detail)
        {
            var t = tally.TryGetValue(key, out var v) ? v : (Right: 0, Total: 0, Ms: new List<long>());
            t.Ms.Add(ms);
            tally[key] = (t.Right + (right ? 1 : 0), t.Total + 1, t.Ms);
            if (!right) sb.AppendLine($"  MISS {key}: {detail}");
        }

        await brain.RespondAsync(Request(data, data.Dynamic[0], "dynamic"));
        await jevBrain.TurnAsync(Request(data, data.Dynamic[0], "dynamic"));

        for (var rep = 0; rep < reps; rep++)
        {
            foreach (var c in data.Dynamic)
            {
                var watch = Stopwatch.StartNew();
                var current = await brain.RespondAsync(Request(data, c, "dynamic"));
                Score("dynamic current mood", c.ExpectMood.Contains(current?.Mood), watch.ElapsedMilliseconds, $"{c.Id} mood {current?.Mood}");
                watch.Restart();
                var viaJev = await jevBrain.TurnAsync(Request(data, c, "dynamic"));
                var ms = watch.ElapsedMilliseconds;
                var d = viaJev.Decision;
                var right = d is not null && c.ExpectMood.Contains(d.Mood) && (c.ExpectKnown is null || c.ExpectKnown == d.Known) && (c.ExpectNoise ?? false) == d.Noise;
                Score("dynamic jev mood+known+noise", right, ms, $"{c.Id} mood {d?.Mood} known {d?.Known} noise {d?.Noise} {viaJev.Failure}");
                if (rep == 0) sb.AppendLine($"{c.Id}\n  current [{current?.Mood}] {current?.Text}\n  jev     [{d?.Mood}{(d?.Known == false ? ", unknown" : "")}{(viaJev.Rewritten ? ", rewritten" : "")}] {viaJev.Text}");
            }

            foreach (var c in data.ScriptedCases)
            {
                var one = new DynamicCase { Id = c.Id, Utterances = [c.Utterance] };
                var watch = Stopwatch.StartNew();
                var current = await brain.RespondAsync(Request(data, one, "scripted"));
                Score("scripted current", current?.LineId == c.ExpectLine, watch.ElapsedMilliseconds, $"{c.Id} line {current?.LineId}");
                watch.Restart();
                var viaJev = await jevBrain.TurnAsync(Request(data, one, "scripted"));
                Score("scripted jev", viaJev.LineId == c.ExpectLine, watch.ElapsedMilliseconds, $"{c.Id} line {viaJev.LineId}");
            }

            foreach (var c in data.AddressCases)
            {
                var answerers = c.Names.Where(n => NpcBrokerService.DecideAddress(NpcNameMatcher.Classify(c.Utterance, n, c.Names), n == c.Facing) ==
                                                   NpcBrokerService.AddressDecision.Answer
                                   )
                                   .ToList();
                Score("address current", answerers.Count == 1 && answerers[0] == c.Expect, 0, $"{c.Id} answered by [{string.Join(",", answerers)}]");
                var (state, questions) = NpcPlainJevDecider.BuildAddress(c.Names, c.Facing, c.Utterance);
                var watch = Stopwatch.StartNew();
                var to = (await jev.AskAsync(state, questions, "bench"))?.Pick("to");
                Score("address jev", to == c.Expect, watch.ElapsedMilliseconds, $"{c.Id} to {to}");
            }
        }

        var head = new StringBuilder();
        foreach (var (key, (right, total, ms)) in tally)
        {
            ms.Sort();
            head.AppendLine($"{key}: {right}/{total}, p50 {ms[ms.Count / 2]}ms, max {ms[^1]}ms");
        }

        var report = head + "\n" + sb;
        Console.WriteLine(report);
        var outPath = Environment.GetEnvironmentVariable("NPC_JEV_BENCH_OUT");
        if (!string.IsNullOrEmpty(outPath)) await File.WriteAllTextAsync(outPath, report);
    }

    private static RespondRequest Request(Cases data, DynamicCase c, string mode)
    {
        var history = new List<NpcHistoryEntry>();
        if (c.WithOverheard) history.Add(new NpcHistoryEntry { Role = "overheard", Speaker = data.Overheard.Speaker, Text = data.Overheard.Text });
        if (c.PriorThreat)
        {
            history.Add(new NpcHistoryEntry { Role = "player", Speaker = "Player", Text = "Give me your money or I break your legs." });
            history.Add(new NpcHistoryEntry { Role = "npc", Text = "Please, I have nothing, take the bread!", Mood = "afraid" });
        }

        return new RespondRequest
        {
            NpcId = "bench-pavel",
            Persona = data.Persona,
            Knowledge = data.Knowledge,
            Mode = mode,
            Scripted = mode == "scripted" ? new NpcScriptedDto { Lines = data.Scripted.Lines, Deflection = data.Scripted.Deflection } : null,
            VoiceId = "bm_george",
            History = history,
            NewTurns = c.Utterances.Select((t, i) => new NpcTurnDto { SpeakerId = "p1", SpeakerName = "Player", Text = t, T = 1_700_000_000_000L + i }).ToList(),
            TextOnly = mode != "scripted"
        };
    }

    private static (NpcBrainService, NpcPlainJevBrain, NpcJevClient) Build(string url)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient());
        var vars = new Mock<IVariablesService>();
        vars.Setup(x => x.GetVariable("CLACKS_URL")).Returns(new DomainVariableItem { Key = "CLACKS_URL", Item = url });
        var voices = new Mock<INpcVoicesContext>();
        voices.Setup(x => x.GetSingle(It.IsAny<Func<DomainNpcVoice, bool>>())).Returns((DomainNpcVoice)null);
        var clacks = new ClacksClient(factory.Object, vars.Object, Mock.Of<IUksfLogger>());
        var jev = new NpcJevClient(factory.Object, vars.Object, Mock.Of<IUksfLogger>());
        return (new NpcBrainService(clacks, voices.Object, Mock.Of<IUksfLogger>()), new NpcPlainJevBrain(jev, clacks, Mock.Of<IUksfLogger>()), jev);
    }

    private sealed class Cases
    {
        public NpcPersona Persona { get; set; }
        public string Knowledge { get; set; }
        public NpcHistoryEntry Overheard { get; set; }
        public NpcScripted Scripted { get; set; }
        public List<DynamicCase> Dynamic { get; set; }
        public List<ScriptedCase> ScriptedCases { get; set; }
        public List<AddressCase> AddressCases { get; set; }
    }

    private sealed class DynamicCase
    {
        public string Id { get; set; }
        public List<string> Utterances { get; set; }
        public bool WithOverheard { get; set; }
        public bool PriorThreat { get; set; }
        public List<string> ExpectMood { get; set; } = [];
        public bool? ExpectKnown { get; set; }
        public bool? ExpectNoise { get; set; }
    }

    private sealed class ScriptedCase
    {
        public string Id { get; set; }
        public string Utterance { get; set; }
        public string ExpectLine { get; set; }
    }

    private sealed class AddressCase
    {
        public string Id { get; set; }
        public List<string> Names { get; set; }
        public string Facing { get; set; }
        public string Utterance { get; set; }
        public string Expect { get; set; }
    }
}
