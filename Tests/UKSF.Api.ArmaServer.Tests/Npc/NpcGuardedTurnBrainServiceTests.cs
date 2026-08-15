using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.ArmaServer.Npc.Services;
using UKSF.Api.Core;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc;

public class NpcGuardedTurnBrainServiceTests
{
    [Fact]
    public async Task Turn_ParsesClassifyAndReplyTogether()
    {
        var clacks = new Mock<IClacksClient>();
        var voices = new Mock<INpcVoicesContext>();
        voices.Setup(x => x.GetSingle(It.IsAny<System.Func<DomainNpcVoice, bool>>())).Returns((DomainNpcVoice)null);
        clacks.Setup(x => x.ChatAsync("npc", It.IsAny<string>(), It.IsAny<string>(), true, It.IsAny<int>(), It.IsAny<double>(), It.IsAny<object>()))
              .ReturnsAsync(
                  new ClacksChatResult
                  {
                      Text =
                          """{"classifications":[{"t":1,"tag":"relevant_question","topicSlot":1,"addressesConcern":false,"ambiguous":false,"reason":"r","evidence":"trucks"}],"text":"Keep your voice down.","mood":"afraid","emote":null,"disclosedFactId":"f1"}""",
                      Model = "m",
                      Node = "n",
                      Ms = 8
                  }
              );

        var sut = new NpcBrainService(clacks.Object, voices.Object, Mock.Of<IUksfLogger>());
        var result = await sut.TurnGuardedAsync(
            new NpcGuardedTurnRequest
            {
                NpcId = "n1",
                VoiceId = "bm_george",
                NewTurns =
                [
                    new NpcTurnDto
                    {
                        SpeakerId = "p",
                        Text = "seen any trucks?",
                        T = 1
                    }
                ]
            }
        );

        result.Classify.Should().NotBeNull();
        result.Classify.Classifications[0].Tag.Should().Be(NpcGuardedTags.RelevantQuestion);
        result.Reply.Ok.Should().BeTrue();
        result.Reply.Text.Should().Be("Keep your voice down.");
        result.Reply.Mood.Should().Be("afraid");
        result.Reply.DisclosedFactId.Should().Be("f1");
    }
}
