using System;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.ArmaServer.Npc.Services;
using UKSF.Api.Core;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc;

public class NpcBrainServiceTests
{
    private static RespondRequest Dynamic() =>
        new()
        {
            NpcId = "n1",
            Persona = new NpcPersona
            {
                Name = "Abu Hassan",
                Role = "farmer",
                Language = "English",
                Mood = "angry",
                AttitudeToPlayers = "hostile"
            },
            Knowledge = "Your field was shelled.",
            Mode = "dynamic",
            VoiceId = "v1",
            History = [],
            NewTurns =
            [
                new NpcTurnDto
                {
                    SpeakerId = "p1",
                    Text = "open up",
                    T = 1
                }
            ]
        };

    private static RespondRequest Scripted()
    {
        var req = Dynamic();
        req.Mode = "scripted";
        req.Scripted = new NpcScriptedDto
        {
            Lines =
            [
                new NpcScriptedLine
                {
                    Id = "ammo",
                    Topic = "ammo",
                    Line = "North building."
                }
            ],
            Deflection = "Ask about supplies."
        };
        return req;
    }

    private static (NpcBrainService service, Mock<IClacksClient> clacks) Build(string text, ClacksSpeakResult speak = null)
    {
        var clacks = new Mock<IClacksClient>();
        clacks.Setup(x => x.ChatAsync(
                         It.IsAny<string>(),
                         It.IsAny<string>(),
                         It.IsAny<string>(),
                         It.IsAny<bool>(),
                         It.IsAny<int>(),
                         It.IsAny<double>(),
                         It.IsAny<object>()
                     )
              )
              .ReturnsAsync(
                  text is null
                      ? null
                      : new ClacksChatResult
                      {
                          Text = text,
                          Node = "server",
                          Model = "qwen2.5-3b",
                          Ms = 1200
                      }
              );
        clacks.Setup(x => x.SpeakAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
              .ReturnsAsync(
                  speak ??
                  new ClacksSpeakResult
                  {
                      AudioBase64 = "V0FW",
                      DurationMs = 2500,
                      Node = "ultron",
                      Model = "kokoro",
                      Ms = 2600
                  }
              );
        return (new NpcBrainService(clacks.Object, Mock.Of<INpcVoicesContext>(), Mock.Of<IUksfLogger>()), clacks);
    }

    [Fact]
    public async Task RespondAsync_Dynamic_CleansTextAndSpeaksIt()
    {
        var (service, clacks) = Build("You said: Get off my land.");
        var result = await service.RespondAsync(Dynamic());

        result.Text.Should().Be("Get off my land.");
        result.LineId.Should().BeNull();
        result.AudioBase64.Should().Be("V0FW");
        result.DurationMs.Should().Be(2500);
        result.Provider.Should().Be("qwen2.5-3b@server");
        clacks.Verify(x => x.ChatAsync(
                          "npc",
                          It.Is<string>(s => s.Contains("Abu Hassan")),
                          It.Is<string>(u => u.Contains("open up")),
                          false,
                          80,
                          0.7,
                          It.IsAny<object>()
                      )
        );
        clacks.Verify(x => x.SpeakAsync("voice", "Get off my land.", "v1"), Times.Once); // speaks the CLEANED text
    }

    [Fact]
    public async Task RespondAsync_Dynamic_EmptyCleanedReply_DoesNotSpeak()
    {
        var (service, clacks) = Build("You said:"); // cleaner strips this to empty
        var result = await service.RespondAsync(Dynamic());

        result.Text.Should().Be(string.Empty);
        result.AudioBase64.Should().BeNull();
        clacks.Verify(x => x.SpeakAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RespondAsync_Dynamic_SpeakFailure_ReturnsTextWithNullAudio()
    {
        var clacks = new Mock<IClacksClient>();
        clacks.Setup(x => x.ChatAsync(
                         It.IsAny<string>(),
                         It.IsAny<string>(),
                         It.IsAny<string>(),
                         It.IsAny<bool>(),
                         It.IsAny<int>(),
                         It.IsAny<double>(),
                         It.IsAny<object>()
                     )
              )
              .ReturnsAsync(
                  new ClacksChatResult
                  {
                      Text = "Go away.",
                      Node = "server",
                      Model = "qwen2.5-3b",
                      Ms = 1200
                  }
              );
        clacks.Setup(x => x.SpeakAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync((ClacksSpeakResult)null);
        var service = new NpcBrainService(clacks.Object, Mock.Of<INpcVoicesContext>(), Mock.Of<IUksfLogger>());

        var result = await service.RespondAsync(Dynamic());

        result.Text.Should().Be("Go away.");
        result.AudioBase64.Should().BeNull();
        result.DurationMs.Should().BeNull();
    }

    [Fact]
    public async Task RespondAsync_Scripted_ResolvesValidLineId()
    {
        var (service, clacks) = Build("{\"lineId\":\"ammo\"}");
        var result = await service.RespondAsync(Scripted());
        result.LineId.Should().Be("ammo");
        result.Text.Should().Be("North building.");
        result.AudioBase64.Should().BeNull();
        // scripted turns use prerendered clips — no synth at respond time
        clacks.Verify(x => x.SpeakAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RespondAsync_Scripted_InvalidChoiceFallsBackToDeflection()
    {
        var (service, _) = Build("{\"lineId\":\"deflection\"}"); // 2B/3B token slip seen in bench
        var result = await service.RespondAsync(Scripted());
        result.LineId.Should().Be(NpcPromptBuilder.Deflection);
        result.Text.Should().Be("Ask about supplies.");
    }

    [Fact]
    public async Task RespondAsync_Scripted_RequestsJsonForcing()
    {
        var (service, clacks) = Build("{\"lineId\":\"ammo\"}");
        await service.RespondAsync(Scripted());
        clacks.Verify(x => x.ChatAsync("npc", It.IsAny<string>(), It.IsAny<string>(), true, 80, 0.7, It.IsAny<object>()));
    }

    [Fact]
    public async Task RespondAsync_NullFromClacks_ReturnsNull()
    {
        var (service, _) = Build(null);
        var result = await service.RespondAsync(Dynamic());
        result.Should().BeNull();
    }

    [Fact]
    public async Task PrerenderAsync_SynthsEveryItem()
    {
        var (service, clacks) = Build("x");
        var result = await service.PrerenderAsync(
            new PrerenderRequest
            {
                VoiceId = "bm_george", Items = [new PrerenderItem { Id = "f0", Text = "hmm" }, new PrerenderItem { Id = "f1", Text = "let me think" }]
            }
        );

        result.Items.Should().HaveCount(2);
        result.Items[0].Id.Should().Be("f0");
        result.Items[0].AudioBase64.Should().Be("V0FW");
        result.Items[0].DurationMs.Should().Be(2500);
        clacks.Verify(x => x.SpeakAsync("voice", "hmm", "bm_george"), Times.Once);
        clacks.Verify(x => x.SpeakAsync("voice", "let me think", "bm_george"), Times.Once);
    }

    [Fact]
    public async Task PrerenderAsync_SkipsFailedItems_AndReturnsTheRest()
    {
        var clacks = new Mock<IClacksClient>();
        clacks.SetupSequence(x => x.SpeakAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
              .ReturnsAsync((ClacksSpeakResult)null)
              .ReturnsAsync(new ClacksSpeakResult { AudioBase64 = "V0FW", DurationMs = 100 });
        var service = new NpcBrainService(clacks.Object, Mock.Of<INpcVoicesContext>(), Mock.Of<IUksfLogger>());

        var result = await service.PrerenderAsync(
            new PrerenderRequest { VoiceId = "v", Items = [new PrerenderItem { Id = "f0", Text = "a" }, new PrerenderItem { Id = "f1", Text = "b" }] }
        );

        result.Items.Should().HaveCount(1);
        result.Items[0].Id.Should().Be("f1");
    }

    [Fact]
    public async Task Dynamic_turn_with_a_registered_variant_speaks_the_variant_slug()
    {
        var clacks = new Mock<IClacksClient>();
        var voices = new Mock<INpcVoicesContext>();
        var logger = new Mock<IUksfLogger>();

        clacks.Setup(x => x.ChatAsync("npc", It.IsAny<string>(), It.IsAny<string>(), false, It.IsAny<int>(), It.IsAny<double>(), It.IsAny<object>()))
              .ReturnsAsync(
                  new ClacksChatResult
                  {
                      Text = "[mood:angry] Get out.",
                      Node = "ultron",
                      Model = "qwen3.5-9b"
                  }
              );
        voices.Setup(x => x.GetSingle(It.IsAny<Func<DomainNpcVoice, bool>>()))
              .Returns((Func<DomainNpcVoice, bool> p) =>
                           p(new DomainNpcVoice { VoiceId = "smuggler_angry" }) ? new DomainNpcVoice { VoiceId = "smuggler_angry" } : null
              );
        clacks.Setup(x => x.SpeakAsync("voice", "Get out.", "smuggler_angry")).ReturnsAsync(new ClacksSpeakResult { AudioBase64 = "AA==", DurationMs = 500 });

        var service = new NpcBrainService(clacks.Object, voices.Object, logger.Object);
        var result = await service.RespondAsync(
            new RespondRequest
            {
                Mode = "dynamic",
                NpcId = "n1",
                VoiceId = "smuggler"
            }
        );

        result.Mood.Should().Be("angry");
        result.Text.Should().Be("Get out.");
        clacks.Verify(x => x.SpeakAsync("voice", "Get out.", "smuggler_angry"), Times.Once);
    }

    [Fact]
    public async Task Dynamic_turn_with_an_unregistered_variant_falls_back_to_base_voice()
    {
        var clacks = new Mock<IClacksClient>();
        var voices = new Mock<INpcVoicesContext>();
        var logger = new Mock<IUksfLogger>();

        clacks.Setup(x => x.ChatAsync("npc", It.IsAny<string>(), It.IsAny<string>(), false, It.IsAny<int>(), It.IsAny<double>(), It.IsAny<object>()))
              .ReturnsAsync(
                  new ClacksChatResult
                  {
                      Text = "[mood:sad] They're gone.",
                      Node = "ultron",
                      Model = "qwen3.5-9b"
                  }
              );
        voices.Setup(x => x.GetSingle(It.IsAny<Func<DomainNpcVoice, bool>>())).Returns((DomainNpcVoice)null);
        clacks.Setup(x => x.SpeakAsync("voice", "They're gone.", "smuggler")).ReturnsAsync(new ClacksSpeakResult { AudioBase64 = "AA==", DurationMs = 400 });

        var service = new NpcBrainService(clacks.Object, voices.Object, logger.Object);
        var result = await service.RespondAsync(
            new RespondRequest
            {
                Mode = "dynamic",
                NpcId = "n1",
                VoiceId = "smuggler"
            }
        );

        result.Mood.Should().Be("sad");
        clacks.Verify(x => x.SpeakAsync("voice", "They're gone.", "smuggler"), Times.Once);
    }

    [Fact]
    public async Task Neutral_mood_speaks_the_base_voice_without_a_registry_lookup()
    {
        var clacks = new Mock<IClacksClient>();
        var voices = new Mock<INpcVoicesContext>();
        var logger = new Mock<IUksfLogger>();

        clacks.Setup(x => x.ChatAsync("npc", It.IsAny<string>(), It.IsAny<string>(), false, It.IsAny<int>(), It.IsAny<double>(), It.IsAny<object>()))
              .ReturnsAsync(
                  new ClacksChatResult
                  {
                      Text = "Move along.",
                      Node = "ultron",
                      Model = "qwen3.5-9b"
                  }
              );
        clacks.Setup(x => x.SpeakAsync("voice", "Move along.", "smuggler")).ReturnsAsync(new ClacksSpeakResult { AudioBase64 = "AA==", DurationMs = 300 });

        var service = new NpcBrainService(clacks.Object, voices.Object, logger.Object);
        var result = await service.RespondAsync(
            new RespondRequest
            {
                Mode = "dynamic",
                NpcId = "n1",
                VoiceId = "smuggler"
            }
        );

        result.Mood.Should().Be("neutral");
        clacks.Verify(x => x.SpeakAsync("voice", "Move along.", "smuggler"), Times.Once);
        voices.Verify(x => x.GetSingle(It.IsAny<Func<DomainNpcVoice, bool>>()), Times.Never);
    }
}
