using System;
using System.Collections.Generic;
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

public class NpcBrokerServiceTests
{
    private readonly Mock<INpcSessionsContext> _sessionsContext = new();
    private readonly Mock<INpcAudioClipsContext> _clipsContext = new();
    private readonly Mock<INpcBrainClient> _brainClient = new();
    private readonly Mock<IGameServerCommandSender> _commandSender = new();
    private readonly Mock<IVariablesService> _variablesService = new();
    private readonly Mock<IUksfLogger> _logger = new();
    private readonly NpcBrokerService _sut;

    public NpcBrokerServiceTests()
    {
        _variablesService.Setup(x => x.GetFeatureState("NPC_BROKER")).Returns(true);
        _sessionsContext.Setup(x => x.GetSingle(It.IsAny<System.Func<DomainNpcSession, bool>>())).Returns((DomainNpcSession)null);
        _clipsContext.Setup(x => x.GetSingle(It.IsAny<System.Func<DomainNpcAudioClip, bool>>())).Returns((DomainNpcAudioClip)null);
        _sessionsContext.Setup(x => x.Add(It.IsAny<DomainNpcSession>())).Returns(Task.CompletedTask);
        _sessionsContext.Setup(x => x.Replace(It.IsAny<DomainNpcSession>())).Returns(Task.CompletedTask);
        _sessionsContext.Setup(x => x.Update(It.IsAny<Expression<Func<DomainNpcSession, bool>>>(), It.IsAny<UpdateDefinition<DomainNpcSession>>()))
                        .Returns(Task.CompletedTask);
        _sessionsContext.Setup(x => x.DeleteMany(It.IsAny<Expression<Func<DomainNpcSession, bool>>>())).Returns(Task.CompletedTask);
        _clipsContext.Setup(x => x.Add(It.IsAny<DomainNpcAudioClip>())).Returns(Task.CompletedTask);
        _clipsContext.Setup(x => x.Replace(It.IsAny<DomainNpcAudioClip>())).Returns(Task.CompletedTask);
        _clipsContext.Setup(x => x.DeleteMany(It.IsAny<Expression<Func<DomainNpcAudioClip, bool>>>())).Returns(Task.CompletedTask);
        _commandSender.Setup(x => x.SendCommandAsync(It.IsAny<int>(), It.IsAny<string>())).Returns(Task.CompletedTask);

        _brainClient.Setup(x => x.PrerenderAsync(It.IsAny<PrerenderRequest>()))
                    .ReturnsAsync(
                        new PrerenderResult
                        {
                            Items =
                            [
                                new PrerenderResultItem
                                {
                                    Id = "f0",
                                    AudioBase64 = "QQ==",
                                    DurationMs = 100
                                },
                                new PrerenderResultItem
                                {
                                    Id = "f1",
                                    AudioBase64 = "QQ==",
                                    DurationMs = 100
                                },
                                new PrerenderResultItem
                                {
                                    Id = "f2",
                                    AudioBase64 = "QQ==",
                                    DurationMs = 100
                                },
                                new PrerenderResultItem
                                {
                                    Id = "f3",
                                    AudioBase64 = "QQ==",
                                    DurationMs = 100
                                }
                            ]
                        }
                    );

        _sut = new NpcBrokerService(
            _sessionsContext.Object,
            _clipsContext.Object,
            _brainClient.Object,
            _commandSender.Object,
            _variablesService.Object,
            _logger.Object
        );
    }

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
    }

    [Fact]
    public async Task RegisterDynamicNpc_UpsertsSession_AndPrerendersWith4Fillers_AndStores4Clips_AndPushes4FillerCommands()
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
                                    Id = "f0",
                                    AudioBase64 = "QQ==",
                                    DurationMs = 100
                                },
                                new PrerenderResultItem
                                {
                                    Id = "f1",
                                    AudioBase64 = "QQ==",
                                    DurationMs = 100
                                },
                                new PrerenderResultItem
                                {
                                    Id = "f2",
                                    AudioBase64 = "QQ==",
                                    DurationMs = 100
                                },
                                new PrerenderResultItem
                                {
                                    Id = "f3",
                                    AudioBase64 = "QQ==",
                                    DurationMs = 100
                                }
                            ]
                        }
                    );

        await _sut.HandleRegisterAsync(5006, MakeRegisterData());

        // Session upserted via Add (no existing session)
        _sessionsContext.Verify(x => x.Add(It.Is<DomainNpcSession>(s => s.NpcId == "npc1" && s.Mode == "dynamic")), Times.Once);
        _sessionsContext.Verify(x => x.Replace(It.IsAny<DomainNpcSession>()), Times.Never);

        // Prerender called with exactly 4 fillers (dynamic mode — no scripted lines)
        capturedRequest.Should().NotBeNull();
        capturedRequest!.VoiceId.Should().Be("bm_george");
        capturedRequest.Items.Should().HaveCount(4);
        capturedRequest.Items.Select(i => i.Id).Should().BeEquivalentTo(["f0", "f1", "f2", "f3"]);

        // 4 clips stored
        _clipsContext.Verify(x => x.Add(It.IsAny<DomainNpcAudioClip>()), Times.Exactly(4));

        // At least 4 filler commands pushed (one per filler — single chunk each for small base64)
        _commandSender.Verify(x => x.SendCommandAsync(5006, It.IsAny<string>()), Times.AtLeast(4));
    }

    [Fact]
    public async Task RegisterScriptedNpc_PrerenderIncludesLinesDeflectionAndFillers_AndStoresAllClips()
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
                                    DurationMs = 200
                                },
                                new PrerenderResultItem
                                {
                                    Id = "__deflection__",
                                    AudioBase64 = "QQ==",
                                    DurationMs = 150
                                },
                                new PrerenderResultItem
                                {
                                    Id = "f0",
                                    AudioBase64 = "QQ==",
                                    DurationMs = 100
                                },
                                new PrerenderResultItem
                                {
                                    Id = "f1",
                                    AudioBase64 = "QQ==",
                                    DurationMs = 100
                                },
                                new PrerenderResultItem
                                {
                                    Id = "f2",
                                    AudioBase64 = "QQ==",
                                    DurationMs = 100
                                },
                                new PrerenderResultItem
                                {
                                    Id = "f3",
                                    AudioBase64 = "QQ==",
                                    DurationMs = 100
                                }
                            ]
                        }
                    );

        await _sut.HandleRegisterAsync(5006, MakeScriptedData());

        capturedRequest.Should().NotBeNull();
        // 1 scripted line + deflection + 4 fillers = 6 items
        capturedRequest!.Items.Should().HaveCount(6);
        capturedRequest.Items.Select(i => i.Id).Should().BeEquivalentTo(["ammo", "__deflection__", "f0", "f1", "f2", "f3"]);

        // 6 clips stored
        _clipsContext.Verify(x => x.Add(It.IsAny<DomainNpcAudioClip>()), Times.Exactly(6));

        // Only filler clips (f0..f3) pushed — not scripted lines or deflection
        _commandSender.Verify(x => x.SendCommandAsync(5006, It.IsAny<string>()), Times.AtLeast(4));
    }

    [Fact]
    public async Task EmptyNpcId_DoesNotWriteToContexts_AndLogsWarning()
    {
        await _sut.HandleRegisterAsync(5006, MakeRegisterData(npcId: ""));

        _sessionsContext.Verify(x => x.Add(It.IsAny<DomainNpcSession>()), Times.Never);
        _sessionsContext.Verify(x => x.Replace(It.IsAny<DomainNpcSession>()), Times.Never);
        _brainClient.Verify(x => x.PrerenderAsync(It.IsAny<PrerenderRequest>()), Times.Never);
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

        await _sut.HandleRegisterAsync(5006, MakeRegisterData());

        _clipsContext.Verify(x => x.Add(It.IsAny<DomainNpcAudioClip>()), Times.Never);
        _commandSender.Verify(x => x.SendCommandAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
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
        pushedCommands.Should().Contain(c => c.Contains("\"f0\""));
        pushedCommands.Should().Contain(c => c.Contains("\"f1\""));
        pushedCommands.Should().Contain(c => c.Contains("\"f2\""));
        pushedCommands.Should().Contain(c => c.Contains("\"f3\""));
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
                LineId = null,
                AudioBase64 = "QQ==",
                DurationMs = 900
            }
        );

        await _sut.HandleTurnAsync(5006, MakeTurnData());

        _commandSender.Verify(x => x.SendCommandAsync(5006, It.Is<string>(s => s.Contains("npc_audio") && s.Contains("turn7"))), Times.AtLeastOnce);
        _sessionsContext.Verify(
            x => x.Update(It.IsAny<Expression<Func<DomainNpcSession, bool>>>(), It.IsAny<UpdateDefinition<DomainNpcSession>>()),
            Times.Once
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
                             AudioBase64 = "QUJD",
                             DurationMs = 1200
                         }
                     );

        await _sut.HandleTurnAsync(5006, MakeTurnData());

        _commandSender.Verify(x => x.SendCommandAsync(5006, It.Is<string>(s => s.Contains("npc_audio") && s.Contains("QUJD"))), Times.AtLeastOnce);
        _sessionsContext.Verify(
            x => x.Update(It.IsAny<Expression<Func<DomainNpcSession, bool>>>(), It.IsAny<UpdateDefinition<DomainNpcSession>>()),
            Times.Once
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
                                 AudioBase64 = "REVG",
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

        _commandSender.Verify(x => x.SendCommandAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        _sessionsContext.Verify(
            x => x.Update(It.IsAny<Expression<Func<DomainNpcSession, bool>>>(), It.IsAny<UpdateDefinition<DomainNpcSession>>()),
            Times.Never
        );
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
}
