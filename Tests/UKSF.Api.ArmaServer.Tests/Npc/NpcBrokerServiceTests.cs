using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Driver;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.ArmaServer.Npc.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc;

public partial class NpcBrokerServiceTests
{
    private static Dictionary<string, object> MakeRegisterData(
        string npcId = "npc1",
        string sessionId = "session1",
        string knowledge = "knows the location",
        string voiceId = "bm_george",
        string mode = "dynamic",
        Dictionary<string, object> persona = null,
        Dictionary<string, object> scripted = null
    )
    {
        return new Dictionary<string, object>
        {
            ["npcId"] = npcId,
            ["sessionId"] = sessionId,
            ["knowledge"] = knowledge,
            ["voiceId"] = voiceId,
            ["mode"] = mode,
            ["persona"] = persona ??
            new Dictionary<string, object>
            {
                ["name"] = "Asad",
                ["role"] = "guard",
                ["language"] = "Arabic",
                ["mood"] = "on edge",
                ["attitudeToPlayers"] = "hostile"
            },
            ["scripted"] = scripted ?? new Dictionary<string, object> { ["lines"] = new List<object>(), ["deflection"] = "I cannot help you." }
        };
    }

    private static Dictionary<string, object> MakeScriptedData(string npcId = "npc1")
    {
        return MakeRegisterData(
            npcId: npcId,
            mode: "scripted",
            scripted: new Dictionary<string, object>
            {
                ["lines"] = new List<object>
                {
                    new Dictionary<string, object>
                    {
                        ["id"] = "ammo",
                        ["topic"] = "ammo cache",
                        ["line"] = "The ammo is in the basement."
                    }
                },
                ["deflection"] = "I cannot help you."
            }
        );
    }

    [Fact]
    public async Task FeatureOff_DoesNotWriteToContextsOrCallBrain()
    {
        _variablesService.Setup(x => x.GetFeatureState("NPC_BROKER")).Returns(false);

        await _sut.HandleRegisterAsync(5006, MakeRegisterData());

        _sessionsContext.Verify(x => x.Add(It.IsAny<DomainNpcSession>()), Times.Never);
        _sessionsContext.Verify(x => x.Replace(It.IsAny<DomainNpcSession>()), Times.Never);
        _clipsContext.Verify(x => x.Add(It.IsAny<DomainNpcAudioClip>()), Times.Never);
        _clipsContext.Verify(x => x.Replace(It.IsAny<DomainNpcAudioClip>()), Times.Never);
        _brainClient.Verify(x => x.PrerenderAsync(It.IsAny<PrerenderRequest>()), Times.Never);
        _clacks.Verify(x => x.WarmAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Register_WarmsNpcAndVoiceRoles()
    {
        await _sut.HandleRegisterAsync(5006, MakeRegisterData());

        _clacks.Verify(x => x.WarmAsync(It.Is<IReadOnlyCollection<string>>(r => r.Contains("pockettts")), NpcWarmKeeper.LeaseMs), Times.Once);
    }

    [Fact]
    public async Task RegisterDynamicNpc_UpsertsSession_AndPushesFillersFromDisk_WithoutPrerendering()
    {
        await _sut.HandleRegisterAsync(5006, MakeRegisterData());

        // Session upserted via Add (no existing session)
        _sessionsContext.Verify(x => x.Add(It.Is<DomainNpcSession>(s => s.NpcId == "npc1" && s.Mode == "dynamic")), Times.Once);
        _sessionsContext.Verify(x => x.Replace(It.IsAny<DomainNpcSession>()), Times.Never);

        // Dynamic mode prerenders nothing; fillers are voice assets read from disk
        _brainClient.Verify(x => x.PrerenderAsync(It.IsAny<PrerenderRequest>()), Times.Never);
        _clipsContext.Verify(x => x.Add(It.IsAny<DomainNpcAudioClip>()), Times.Never);
        _voiceStore.Verify(x => x.ReadAsync(It.IsAny<string>()), Times.Exactly(FillerIds.Count));
        _commandSender.Verify(x => x.SendCommandAsync(5006, It.Is<string>(c => c.Contains("npc_filler"))), Times.AtLeast(FillerIds.Count));
    }

    [Fact]
    public async Task RegisterScriptedNpc_PrerendersLinesAndDeflection_AndPushesFillersFromDisk()
    {
        PrerenderRequest capturedRequest = null;
        _brainClient.Setup(x => x.PrerenderAsync(It.IsAny<PrerenderRequest>()))
                    .Callback<PrerenderRequest>(r => capturedRequest = r)
                    .ReturnsAsync(
                        new PrerenderResult
                        {
                            Items =
                            [
                                new PrerenderResultItem
                                {
                                    Id = "ammo",
                                    AudioBase64 = "QQ==",
                                    DurationMs = 100
                                },
                                new PrerenderResultItem
                                {
                                    Id = "__deflection__",
                                    AudioBase64 = "QQ==",
                                    DurationMs = 100
                                }
                            ]
                        }
                    );

        await _sut.HandleRegisterAsync(5006, MakeScriptedData());

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Items.Select(i => i.Id).Should().BeEquivalentTo("ammo", "__deflection__");

        _clipsContext.Verify(x => x.Add(It.Is<DomainNpcAudioClip>(c => c.FilePath.EndsWith(".wav"))), Times.Exactly(2));
        _audioStore.Verify(x => x.SaveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>()), Times.Exactly(2));

        // Filler clips pushed from disk — not the scripted lines or the deflection
        _voiceStore.Verify(x => x.ReadAsync(It.IsAny<string>()), Times.Exactly(FillerIds.Count));
        _commandSender.Verify(x => x.SendCommandAsync(5006, It.Is<string>(c => c.Contains("npc_filler"))), Times.AtLeast(FillerIds.Count));
    }

    [Fact]
    public async Task EmptyNpcId_DoesNotWriteToContexts_AndLogsWarning()
    {
        await _sut.HandleRegisterAsync(5006, MakeRegisterData(npcId: ""));

        _sessionsContext.Verify(x => x.Add(It.IsAny<DomainNpcSession>()), Times.Never);
        _sessionsContext.Verify(x => x.Replace(It.IsAny<DomainNpcSession>()), Times.Never);
        _brainClient.Verify(x => x.PrerenderAsync(It.IsAny<PrerenderRequest>()), Times.Never);
        _clacks.Verify(x => x.WarmAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<int>()), Times.Never);
        _logger.Verify(x => x.LogWarning(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ExistingSession_ReplacesRatherThanAdds()
    {
        var existingSession = new DomainNpcSession { Id = "existing-id", NpcId = "npc1" };
        _sessionsContext.Setup(x => x.GetSingle(It.IsAny<System.Func<DomainNpcSession, bool>>())).Returns(existingSession);

        await _sut.HandleRegisterAsync(5006, MakeRegisterData());

        _sessionsContext.Verify(x => x.Replace(It.Is<DomainNpcSession>(s => s.Id == "existing-id")), Times.Once);
        _sessionsContext.Verify(x => x.Add(It.IsAny<DomainNpcSession>()), Times.Never);
    }

    [Fact]
    public async Task PrerenderReturnsNull_LogsWarning_AndDoesNotStoreClips()
    {
        _brainClient.Setup(x => x.PrerenderAsync(It.IsAny<PrerenderRequest>())).ReturnsAsync((PrerenderResult)null);

        await _sut.HandleRegisterAsync(5006, MakeScriptedData());

        _clipsContext.Verify(x => x.Add(It.IsAny<DomainNpcAudioClip>()), Times.Never);
        _audioStore.Verify(x => x.SaveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>()), Times.Never);
        _logger.Verify(x => x.LogWarning(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task RegisterDynamicNpc_FillerPushCommands_ContainCorrectFillerIds()
    {
        var pushedCommands = new List<string>();
        _commandSender.Setup(x => x.SendCommandAsync(5006, It.IsAny<string>()))
                      .Callback<int, string>((_, cmd) => pushedCommands.Add(cmd))
                      .Returns(Task.CompletedTask);

        await _sut.HandleRegisterAsync(5006, MakeRegisterData());

        // Each command should be a npc_filler envelope containing the filler id
        pushedCommands.Should().HaveCountGreaterThanOrEqualTo(4);
        pushedCommands.Should().Contain(c => c.Contains("\"npc_filler\""));
        foreach (var fillerId in FillerIds)
        {
            pushedCommands.Should().Contain(c => c.Contains($"\"{fillerId}\""));
        }
    }

    private static DomainNpcSession MakeDynamicSession(string npcId = "npc1", string sessionId = "session1", string voiceId = "bm_george") =>
        new()
        {
            NpcId = npcId,
            SessionId = sessionId,
            VoiceId = voiceId,
            Mode = "dynamic",
            Persona = new NpcPersona
            {
                Name = "Asad",
                Role = "guard",
                Language = "Arabic",
                Mood = "on edge",
                AttitudeToPlayers = "hostile"
            },
            Knowledge = "knows the location",
            Scripted = new NpcScripted(),
            History = []
        };

    private static DomainNpcSession MakeScriptedSession(string npcId = "npc1", string sessionId = "session1", string voiceId = "bm_george") =>
        new()
        {
            NpcId = npcId,
            SessionId = sessionId,
            VoiceId = voiceId,
            Mode = "scripted",
            Persona = new NpcPersona
            {
                Name = "Asad",
                Role = "guard",
                Language = "Arabic",
                Mood = "on edge",
                AttitudeToPlayers = "hostile"
            },
            Knowledge = "knows the location",
            Scripted = new NpcScripted
            {
                Lines =
                [
                    new NpcScriptedLine
                    {
                        Id = "ammo",
                        Topic = "ammo cache",
                        Line = "The ammo is in the basement."
                    }
                ],
                Deflection = "I cannot help you."
            },
            History = []
        };

    private static Dictionary<string, object> MakeTurnData(
        string npcId = "npc1",
        string sessionId = "session1",
        string turnId = "turn7",
        List<object> newTurns = null
    ) =>
        new()
        {
            ["npcId"] = npcId,
            ["sessionId"] = sessionId,
            ["turnId"] = turnId,
            ["gazeAddressed"] = true,
            ["newTurns"] = newTurns ??
            [
                new Dictionary<string, object>
                {
                    ["speakerId"] = "76561",
                    ["text"] = "where is the ammo?",
                    ["t"] = 1700000000000L
                }
            ]
        };

    [Fact]
    public async Task HandleTurnAsync_DynamicTurn_PushesNpcAudioCommand_AndUpdatesHistory()
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
        _clacks.Setup(x => x.SpeakStreamAsync("npc-voice", "go away", "bm_george", It.IsAny<Func<string, Task>>()))
               .Returns(async (string r, string t, string v, Func<string, Task> onFrame) => await onFrame("QQ=="));

        await _sut.HandleTurnAsync(5006, MakeTurnData());

        _commandSender.Verify(x => x.SendCommandAsync(5006, It.Is<string>(s => s.Contains("npc_audio_frame") && s.Contains("turn7"))), Times.Once);
        _commandSender.Verify(x => x.SendCommandAsync(5006, It.Is<string>(s => s.Contains("npc_audio_end") && s.Contains("turn7"))), Times.Once);
        _sessionsContext.Verify(
            x => x.Update(It.IsAny<Expression<Func<DomainNpcSession, bool>>>(), It.IsAny<UpdateDefinition<DomainNpcSession>>()),
            Times.Exactly(2) // own history + overheard write to the other sessions
        );
    }

    [Fact]
    public async Task HandleTurnAsync_ScriptedTurn_LooksUpClipByLineId_AndPushesIt()
    {
        _sessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainNpcSession, bool>>())).Returns(MakeScriptedSession());
        _brainClient.Setup(x => x.RespondAsync(It.IsAny<RespondRequest>()))
                    .ReturnsAsync(
                        new RespondResult
                        {
                            Text = "The ammo is in the basement.",
                            LineId = "ammo",
                            AudioBase64 = null,
                            DurationMs = null
                        }
                    );
        _clipsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainNpcAudioClip, bool>>()))
                     .Returns(
                         new DomainNpcAudioClip
                         {
                             VoiceId = "bm_george",
                             ClipId = "ammo",
                             FilePath = "2026-06-07/session1_npc1_ammo.wav",
                             DurationMs = 1200
                         }
                     );

        await _sut.HandleTurnAsync(5006, MakeTurnData());

        _commandSender.Verify(x => x.SendCommandAsync(5006, It.Is<string>(s => s.Contains("npc_audio") && s.Contains("QUJD"))), Times.AtLeastOnce);
        _sessionsContext.Verify(
            x => x.Update(It.IsAny<Expression<Func<DomainNpcSession, bool>>>(), It.IsAny<UpdateDefinition<DomainNpcSession>>()),
            Times.Exactly(2) // own history + overheard write to the other sessions
        );
    }

    [Fact]
    public async Task HandleTurnAsync_ScriptedDeflection_LooksUpDeflectionClipWhenLineIdNull()
    {
        _sessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainNpcSession, bool>>())).Returns(MakeScriptedSession());
        _brainClient.Setup(x => x.RespondAsync(It.IsAny<RespondRequest>()))
        .ReturnsAsync(
            new RespondResult
            {
                Text = "I cannot help you.",
                LineId = null,
                AudioBase64 = null,
                DurationMs = null
            }
        );

        DomainNpcAudioClip capturedClipLookup = null;
        string capturedClipId = null;
        _clipsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainNpcAudioClip, bool>>()))
                     .Returns<Func<DomainNpcAudioClip, bool>>(predicate =>
                         {
                             var deflectionClip = new DomainNpcAudioClip
                             {
                                 SessionId = "session1",
                                 NpcId = "npc1",
                                 VoiceId = "bm_george",
                                 ClipId = "__deflection__",
                                 FilePath = "2026-06-07/session1_npc1___deflection__.wav",
                                 DurationMs = 800
                             };
                             capturedClipLookup = predicate(deflectionClip) ? deflectionClip : null;
                             capturedClipId = deflectionClip.ClipId;
                             return capturedClipLookup;
                         }
                     );

        await _sut.HandleTurnAsync(5006, MakeTurnData());

        capturedClipLookup.Should().NotBeNull("deflection clip should be looked up when LineId is null");
        _commandSender.Verify(x => x.SendCommandAsync(5006, It.Is<string>(s => s.Contains("npc_audio"))), Times.AtLeastOnce);
    }

    [Fact]
    public async Task HandleTurnAsync_MissingSession_DoesNotSendCommand_AndLogsWarning()
    {
        _sessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainNpcSession, bool>>())).Returns((DomainNpcSession)null);

        await _sut.HandleTurnAsync(5006, MakeTurnData());

        _commandSender.Verify(x => x.SendCommandAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        _sessionsContext.Verify(
            x => x.Update(It.IsAny<Expression<Func<DomainNpcSession, bool>>>(), It.IsAny<UpdateDefinition<DomainNpcSession>>()),
            Times.Never
        );
        _logger.Verify(x => x.LogWarning(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task HandleTurnAsync_BrainReturnsNull_DoesNotSendCommand_AndDoesNotUpdateHistory()
    {
        _sessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainNpcSession, bool>>())).Returns(MakeDynamicSession());
        _brainClient.Setup(x => x.RespondAsync(It.IsAny<RespondRequest>())).ReturnsAsync((RespondResult)null);

        await _sut.HandleTurnAsync(5006, MakeTurnData());

        // Only the turn-cancel goes out — the filler loop must stop, not pad a dead turn.
        _commandSender.Verify(x => x.SendCommandAsync(It.IsAny<int>(), It.Is<string>(c => c.Contains("npc_turn_cancel"))), Times.Once);
        _sessionsContext.Verify(
            x => x.Update(It.IsAny<Expression<Func<DomainNpcSession, bool>>>(), It.IsAny<UpdateDefinition<DomainNpcSession>>()),
            Times.Never
        );
    }

    [Fact]
    public async Task HandleTurnAsync_UnnamedAndNotLookedAt_StaysSilent_AndCancelsTheFillerLoop()
    {
        _sessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainNpcSession, bool>>())).Returns(MakeDynamicSession());
        var data = MakeTurnData();
        data["gazeAddressed"] = "false";

        await _sut.HandleTurnAsync(5006, data);

        // Every talkable NPC in earshot gets the utterance; an unnamed one belongs to
        // whoever was being looked at, and the rest must stop their fillers.
        _brainClient.Verify(x => x.RespondAsync(It.IsAny<RespondRequest>()), Times.Never);
        _commandSender.Verify(x => x.SendCommandAsync(5006, It.Is<string>(c => c.Contains("npc_turn_cancel"))), Times.Once);
    }

    [Fact]
    public async Task HandleTurnAsync_AllNewTurnsWhitespaceText_DropsAll_AndDoesNotCallBrain()
    {
        _sessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainNpcSession, bool>>())).Returns(MakeDynamicSession());

        var turnsWithWhitespace = new List<object>
        {
            new Dictionary<string, object>
            {
                ["speakerId"] = "76561",
                ["text"] = "   ",
                ["t"] = 1700000000000L
            },
            new Dictionary<string, object>
            {
                ["speakerId"] = "76562",
                ["text"] = "\t\n",
                ["t"] = 1700000000001L
            }
        };

        await _sut.HandleTurnAsync(5006, MakeTurnData(newTurns: turnsWithWhitespace));

        _brainClient.Verify(x => x.RespondAsync(It.IsAny<RespondRequest>()), Times.Never);
        _commandSender.Verify(x => x.SendCommandAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task HandleMissionEndedAsync_DeletesBothContextsForSession()
    {
        await _sut.HandleMissionEndedAsync("sess1");

        _sessionsContext.Verify(x => x.DeleteMany(It.IsAny<Expression<Func<DomainNpcSession, bool>>>()), Times.Once);
        _clipsContext.Verify(x => x.DeleteMany(It.IsAny<Expression<Func<DomainNpcAudioClip, bool>>>()), Times.Once);
    }

    [Fact]
    public async Task HandleMissionEndedAsync_EmptySessionId_DoesNotCallDeleteMany()
    {
        await _sut.HandleMissionEndedAsync(string.Empty);

        _sessionsContext.Verify(x => x.DeleteMany(It.IsAny<Expression<Func<DomainNpcSession, bool>>>()), Times.Never);
        _clipsContext.Verify(x => x.DeleteMany(It.IsAny<Expression<Func<DomainNpcAudioClip, bool>>>()), Times.Never);
    }

    [Fact]
    public async Task HandleTurnAsync_DynamicTurn_StreamsFramesThenEnd()
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
        _clacks.Setup(x => x.SpeakStreamAsync("npc-voice", "go away", "bm_george", It.IsAny<Func<string, Task>>()))
               .Returns(async (string r, string t, string v, Func<string, Task> onFrame) =>
                   {
                       await onFrame("QQ==");
                       await onFrame("Qg==");
                   }
               );

        await _sut.HandleTurnAsync(5006, MakeTurnData());

        _commandSender.Verify(x => x.SendCommandAsync(5006, It.Is<string>(s => s.Contains("npc_audio_frame") && s.Contains("turn7"))), Times.Exactly(2));
        _commandSender.Verify(x => x.SendCommandAsync(5006, It.Is<string>(s => s.Contains("npc_audio_end") && s.Contains("turn7"))), Times.Once);
        _audioStore.Verify(x => x.SaveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>()), Times.Never);
    }

    [Fact]
    public async Task HandleTurnAsync_DynamicTurn_StreamFailureCancelsWithoutHistory()
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
               .ThrowsAsync(new IOException("mesh down"));

        await _sut.HandleTurnAsync(5006, MakeTurnData());

        _commandSender.Verify(x => x.SendCommandAsync(5006, It.Is<string>(s => s.Contains("npc_turn_cancel"))), Times.Once);
        _commandSender.Verify(x => x.SendCommandAsync(5006, It.Is<string>(s => s.Contains("npc_audio_end"))), Times.Never);
        _sessionsContext.Verify(
            x => x.Update(It.IsAny<Expression<Func<DomainNpcSession, bool>>>(), It.IsAny<UpdateDefinition<DomainNpcSession>>()),
            Times.Never
        );
    }
}
