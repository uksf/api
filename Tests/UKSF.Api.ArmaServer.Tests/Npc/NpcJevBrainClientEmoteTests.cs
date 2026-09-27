using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.ArmaServer.Npc.Services;
using UKSF.Api.Core;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc;

public class NpcJevBrainClientEmoteTests
{
    [Fact]
    public async Task Plain_PassesWriterEmoteAndJevDecision()
    {
        var jev = new Mock<INpcJevClient>();
        var clacks = new Mock<IClacksClient>();
        var logger = Mock.Of<IUksfLogger>();
        var sut = new NpcJevBrainClient(
            new NpcBrainService(clacks.Object, new Mock<INpcVoicesContext>().Object, logger),
            new NpcGuardedJevBrain(jev.Object, clacks.Object, logger),
            new NpcPlainJevBrain(jev.Object, clacks.Object, logger),
            jev.Object,
            logger
        );
        jev.Setup(x => x.AskAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, JevQuestion>>(), It.IsAny<string>()))
           .ReturnsAsync((string _, Dictionary<string, JevQuestion> q, string _) => new JevResult
               {
                   Answers = q.Keys.ToDictionary(
                       k => k,
                       k => k == "mood"
                           ? new JevAnswer { Type = "choice", Choice = "happy" }
                           : new JevAnswer { Type = "noul", Noul = k == "known" ? 0.9 : 0.1 }
                   )
               }
           );
        clacks.Setup(x => x.ChatAsync("npc", It.IsAny<string>(), It.IsAny<string>(), true, It.IsAny<int>(), It.IsAny<double>(), It.IsAny<object>()))
              .ReturnsAsync(new ClacksChatResult { Text = "{\"text\":\"Two coins.\",\"emote\":\"taps the bread\"}", Ms = 600 });

        var result = await sut.RespondAsync(
            new RespondRequest
            {
                NpcId = "pavel",
                Persona = new NpcPersona
                {
                    Name = "Pavel",
                    Role = "trader",
                    Language = "English",
                    Mood = "chatty",
                    AttitudeToPlayers = "friendly"
                },
                Knowledge = "Sells bread.",
                NewTurns =
                [
                    new NpcTurnDto
                    {
                        SpeakerName = "Player",
                        Text = "How much for the bread?",
                        T = 1
                    }
                ],
                TextOnly = true
            }
        );

        result.Text.Should().Be("Two coins.");
        result.Emote.Should().Be("taps the bread");
        result.Decision.Should().BeOfType<NpcPlainDecision>().Which.Mood.Should().Be("happy");
    }
}
