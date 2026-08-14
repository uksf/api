using System;
using System.Threading.Tasks;
using Moq;
using UKSF.Api.ArmaServer.Npc.Models;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc;

public partial class NpcBrokerServiceTests
{
    [Fact]
    public async Task HandleTurnAsync_ConversationAnswer_SendsDebugStateWithProvider()
    {
        _sessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainNpcSession, bool>>())).Returns(MakeDynamicSession());
        _brainClient.Setup(x => x.RespondAsync(It.IsAny<RespondRequest>()))
        .ReturnsAsync(
            new RespondResult
            {
                Text = "go away",
                Mood = "neutral",
                VoiceId = "bm_george",
                Provider = "luna@ultron"
            }
        );
        _clacks.Setup(x => x.SpeakStreamAsync("npc-voice", "go away", "bm_george", It.IsAny<Func<string, Task>>()))
               .Returns(async (string _, string _, string _, Func<string, Task> onFrame) => await onFrame("QQ=="));

        await _sut.HandleTurnAsync(5006, MakeTurnData());

        _commandSender.Verify(
            x => x.SendCommandAsync(5006, It.Is<string>(c => c.Contains("\"npc_debug_state\"") && c.Contains("\"luna@ultron\"") && c.Contains("\"answer\""))),
            Times.Once
        );
    }

    [Fact]
    public async Task HandleTurnAsync_ConversationNone_SendsDebugStateNone()
    {
        _sessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainNpcSession, bool>>())).Returns(MakeDynamicSession());
        _brainClient.Setup(x => x.RespondAsync(It.IsAny<RespondRequest>())).ReturnsAsync(new RespondResult { Text = "[none]", Provider = "luna@ultron" });

        await _sut.HandleTurnAsync(5006, MakeTurnData());

        _commandSender.Verify(
            x => x.SendCommandAsync(5006, It.Is<string>(c => c.Contains("\"npc_debug_state\"") && c.Contains("\"none\"") && c.Contains("\"luna@ultron\""))),
            Times.Once
        );
        _commandSender.Verify(x => x.SendCommandAsync(5006, It.Is<string>(c => c.Contains("npc_turn_cancel"))), Times.Once);
    }

    [Fact]
    public async Task HandleTurnAsync_ConversationSilent_SendsDebugStateStaySilent()
    {
        _sessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainNpcSession, bool>>())).Returns(MakeDynamicSession());
        var data = MakeTurnData();
        data["gazeAddressed"] = "false";

        await _sut.HandleTurnAsync(5006, data);

        _commandSender.Verify(
            x => x.SendCommandAsync(5006, It.Is<string>(c => c.Contains("\"npc_debug_state\"") && c.Contains("\"stay_silent\""))),
            Times.Once
        );
        _brainClient.Verify(x => x.RespondAsync(It.IsAny<RespondRequest>()), Times.Never);
    }

    [Fact]
    public async Task HandleTurnAsync_ScriptedTurn_MissingClipFile_SendsDebugStateOnly()
    {
        _sessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainNpcSession, bool>>())).Returns(MakeScriptedSession());
        _brainClient.Setup(x => x.RespondAsync(It.IsAny<RespondRequest>()))
                    .ReturnsAsync(new RespondResult { Text = "The ammo is in the basement.", LineId = "ammo" });
        _clipsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainNpcAudioClip, bool>>()))
                     .Returns(
                         new DomainNpcAudioClip
                         {
                             ClipId = "ammo",
                             FilePath = "2026-06-07/gone.wav",
                             DurationMs = 1200
                         }
                     );
        _audioStore.Setup(x => x.ReadAsync("2026-06-07/gone.wav")).ReturnsAsync((byte[])null);

        await _sut.HandleTurnAsync(5006, MakeTurnData());

        _commandSender.Verify(x => x.SendCommandAsync(It.IsAny<int>(), It.Is<string>(c => !c.Contains("npc_debug_state"))), Times.Never);
        _commandSender.Verify(x => x.SendCommandAsync(5006, It.Is<string>(c => c.Contains("\"npc_debug_state\""))), Times.Once);
        _logger.Verify(x => x.LogWarning(It.IsAny<string>()), Times.Once);
    }
}
