using System;
using System.IO;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MongoDB.Driver;
using Moq;
using UKSF.Api.ArmaServer.Npc.Models;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc;

public partial class NpcBrokerServiceTests
{
    [Fact]
    public async Task HandleTurnAsync_DynamicTurn_PartialStreamFailureClosesWithoutHistory()
    {
        _sessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainNpcSession, bool>>())).Returns(MakeDynamicSession());
        _brainClient.Setup(x => x.RespondAsync(It.IsAny<RespondRequest>()))
        .ReturnsAsync(
            new RespondResult
            {
                Text = "go away",
                Mood = "neutral",
                VoiceId = "bm_george"
            }
        );
        _clacks.Setup(x => x.SpeakStreamAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<string, Task>>()))
               .Returns(async (string _, string _, string _, Func<string, Task> onFrame) =>
                   {
                       await onFrame("QQ==");
                       throw new IOException("stream failed");
                   }
               );

        await _sut.HandleTurnAsync(5006, MakeTurnData());

        _commandSender.Verify(x => x.SendCommandAsync(5006, It.Is<string>(s => s.Contains("npc_audio_frame"))), Times.Once);
        _commandSender.Verify(x => x.SendCommandAsync(5006, It.Is<string>(s => s.Contains("npc_audio_end"))), Times.Once);
        _sessionsContext.Verify(
            x => x.Update(It.IsAny<Expression<Func<DomainNpcSession, bool>>>(), It.IsAny<UpdateDefinition<DomainNpcSession>>()),
            Times.Never
        );
    }

    [Fact]
    public async Task HandleTurnAsync_DynamicTurn_EmptyTextCancelsWithoutHistory()
    {
        _sessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainNpcSession, bool>>())).Returns(MakeDynamicSession());
        _brainClient.Setup(x => x.RespondAsync(It.IsAny<RespondRequest>())).ReturnsAsync(new RespondResult { Text = "", VoiceId = "bm_george" });

        await _sut.HandleTurnAsync(5006, MakeTurnData());

        _commandSender.Verify(x => x.SendCommandAsync(5006, It.Is<string>(s => s.Contains("npc_turn_cancel"))), Times.Once);
        _clacks.Verify(x => x.SpeakStreamAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<string, Task>>()), Times.Never);
        _sessionsContext.Verify(
            x => x.Update(It.IsAny<Expression<Func<DomainNpcSession, bool>>>(), It.IsAny<UpdateDefinition<DomainNpcSession>>()),
            Times.Never
        );
    }
}
