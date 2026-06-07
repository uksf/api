using System.Threading.Tasks;
using FluentAssertions;
using Moq;
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

    private static (NpcBrainService service, Mock<IClacksClient> clacks) Build(string text)
    {
        var clacks = new Mock<IClacksClient>();
        clacks.Setup(x => x.ChatAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<double>()))
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
        return (new NpcBrainService(clacks.Object, Mock.Of<IUksfLogger>()), clacks);
    }

    [Fact]
    public async Task RespondAsync_Dynamic_CleansTextAndReturnsNullAudio()
    {
        var (service, clacks) = Build("You said: Get off my land.");
        var result = await service.RespondAsync(Dynamic());

        result.Text.Should().Be("Get off my land.");
        result.LineId.Should().BeNull();
        result.AudioBase64.Should().BeNull();
        result.Provider.Should().Be("qwen2.5-3b@server");
        clacks.Verify(x => x.ChatAsync("npc", It.Is<string>(s => s.Contains("Abu Hassan")), It.Is<string>(u => u.Contains("open up")), false, 80, 0.7));
    }

    [Fact]
    public async Task RespondAsync_Scripted_ResolvesValidLineId()
    {
        var (service, _) = Build("{\"lineId\":\"ammo\"}");
        var result = await service.RespondAsync(Scripted());
        result.LineId.Should().Be("ammo");
        result.Text.Should().Be("North building.");
        result.AudioBase64.Should().BeNull();
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
        clacks.Verify(x => x.ChatAsync("npc", It.IsAny<string>(), It.IsAny<string>(), true, 80, 0.7));
    }

    [Fact]
    public async Task RespondAsync_NullFromClacks_ReturnsNull()
    {
        var (service, _) = Build(null);
        var result = await service.RespondAsync(Dynamic());
        result.Should().BeNull();
    }

    [Fact]
    public async Task PrerenderAsync_ReturnsNullWhileTtsStubbed()
    {
        var (service, _) = Build("x");
        var result = await service.PrerenderAsync(new PrerenderRequest());
        result.Should().BeNull();
    }
}
