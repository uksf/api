using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.ArmaServer.Npc.Services;
using UKSF.Api.Core;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc;

public class NpcGuardedBrainServiceTests
{
    private readonly Mock<IClacksClient> _clacks = new();
    private readonly Mock<INpcVoicesContext> _voices = new();
    private readonly Mock<IUksfLogger> _logger = new();
    private readonly NpcBrainService _sut;

    public NpcGuardedBrainServiceTests()
    {
        _voices.Setup(x => x.GetSingle(It.IsAny<System.Func<DomainNpcVoice, bool>>())).Returns((DomainNpcVoice)null);
        _sut = new NpcBrainService(_clacks.Object, _voices.Object, _logger.Object);
    }

    [Fact]
    public async Task Turn_ParsesCombinedJson()
    {
        _clacks.Setup(x => x.ChatAsync("npc", It.IsAny<string>(), It.IsAny<string>(), true, It.IsAny<int>(), It.IsAny<double>(), It.IsAny<object>()))
               .ReturnsAsync(
                   new ClacksChatResult
                   {
                       Text =
                           """{"classifications":[{"t":1,"tag":"relevant_question","topicSlot":1,"addressesConcern":false,"ambiguous":false,"reason":"r","evidence":"trucks"}],"text":"A grey pickup came through this morning.","mood":"afraid","emote":null,"disclosedFactId":"1"}""",
                       Model = "m",
                       Node = "n",
                       Ms = 9
                   }
               );

        var result = await _sut.TurnGuardedAsync(MakeTurn());
        result.Classify.Should().NotBeNull();
        result.Classify!.Classifications.Should().ContainSingle();
        result.Reply.Ok.Should().BeTrue();
        result.Reply.Text.Should().Be("A grey pickup came through this morning.");
        result.Reply.DisclosedFactId.Should().Be("1");
    }

    [Fact]
    public async Task Turn_MalformedJson_ReturnsFailure_NoThrow()
    {
        _clacks.Setup(x => x.ChatAsync("npc", It.IsAny<string>(), It.IsAny<string>(), true, It.IsAny<int>(), It.IsAny<double>(), It.IsAny<object>()))
               .ReturnsAsync(new ClacksChatResult { Text = "{bad", Model = "m", Node = "n" });

        var result = await _sut.TurnGuardedAsync(MakeTurn());
        result.Reply.Ok.Should().BeFalse();
        result.Reply.Failure.Should().Contain("parse");
    }

    [Fact]
    public async Task Turn_NullClacks_ReturnsFailure()
    {
        _clacks.Setup(x => x.ChatAsync(
                          It.IsAny<string>(),
                          It.IsAny<string>(),
                          It.IsAny<string>(),
                          It.IsAny<bool>(),
                          It.IsAny<int>(),
                          It.IsAny<double>(),
                          It.IsAny<object>()
                      )
               )
               .ReturnsAsync((ClacksChatResult)null);

        var result = await _sut.TurnGuardedAsync(MakeTurn());
        result.Reply.Ok.Should().BeFalse();
    }

    private static NpcGuardedTurnRequest MakeTurn() =>
        new()
        {
            NpcId = "n1",
            Persona = new NpcPersona { Name = "Tomas", Role = "farmer", Language = "English", Mood = "wary", AttitudeToPlayers = "cautious" },
            Knowledge = "local farmer brief",
            Concern = "family",
            VoiceId = "bm_george",
            State = new NpcGuardedState(),
            NextFact = new NpcGuardedFact { Id = "1", Topic = "traffic", Text = "Trucks have been rolling past the farm after dark." },
            NewTurns = [new NpcTurnDto { SpeakerId = "p", Text = "seen any trucks?", T = 1 }]
        };
}
