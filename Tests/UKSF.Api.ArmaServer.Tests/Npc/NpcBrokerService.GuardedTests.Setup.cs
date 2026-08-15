using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MongoDB.Driver;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.ArmaServer.Npc.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.Tests.Npc;

public partial class NpcBrokerServiceGuardedTests
{
    private readonly Mock<INpcSessionsContext> _sessions = new();
    private readonly Mock<INpcAudioClipsContext> _clips = new();
    private readonly Mock<INpcBrainClient> _brain = new();
    private readonly Mock<IClacksClient> _clacks = new();
    private readonly Mock<IGameServerCommandSender> _commands = new();
    private readonly Mock<INpcAudioStore> _audio = new();
    private readonly Mock<INpcVoiceStore> _voiceStore = new();
    private readonly Mock<INpcVoicesContext> _voices = new();
    private readonly Mock<IVariablesService> _vars = new();
    private readonly Mock<IUksfLogger> _logger = new();
    private readonly NpcBrokerService _sut;
    private DomainNpcSession _session;
    private int _updates;

    public NpcBrokerServiceGuardedTests()
    {
        _vars.Setup(x => x.GetFeatureState("NPC_BROKER")).Returns(true);
        _sessions.Setup(x => x.GetSingle(It.IsAny<Func<DomainNpcSession, bool>>())).Returns(() => _session);
        _sessions.Setup(x => x.Get(It.IsAny<Func<DomainNpcSession, bool>>())).Returns(() => _session is null ? [] : [_session]);
        _sessions.Setup(x => x.Update(It.IsAny<Expression<Func<DomainNpcSession, bool>>>(), It.IsAny<UpdateDefinition<DomainNpcSession>>()))
                 .Callback(() => _updates++)
                 .Returns(Task.CompletedTask);
        _commands.Setup(x => x.SendCommandAsync(It.IsAny<int>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        _voices.Setup(x => x.GetSingle(It.IsAny<Func<DomainNpcVoice, bool>>())).Returns((DomainNpcVoice)null);
        _clacks.Setup(x => x.SpeakStreamAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<string, Task>>()))
               .Returns(async (string _, string _, string _, Func<string, Task> onFrame) => await onFrame("QQ=="));

        _sut = new NpcBrokerService(
            _sessions.Object,
            _clips.Object,
            _brain.Object,
            _clacks.Object,
            _commands.Object,
            _audio.Object,
            _voiceStore.Object,
            _voices.Object,
            _vars.Object,
            _logger.Object
        );
    }

    private NpcGuardedClassification[] _lastClassify;
    private NpcGuardedReplyResult _lastReply;

    private void SetupClassify(params NpcGuardedClassification[] classifications)
    {
        _lastClassify = classifications;
        _brain.Setup(x => x.ClassifyGuardedAsync(It.IsAny<NpcGuardedClassifyRequest>()))
              .ReturnsAsync(new NpcGuardedClassifyResult { Classifications = classifications.ToList(), Ms = 5 });
        WireTurn();
    }

    private void SetupReply(string text, string mood, string emote, string factId)
    {
        _lastReply = OkReply(text, factId, mood, emote);
        WireTurn();
    }

    private void SetupFailedReply(string failure = "null model")
    {
        _lastReply = new NpcGuardedReplyResult { Ok = false, Failure = failure };
        WireTurn();
    }

    private void SetupTurnThrow(Exception exception)
    {
        _brain.Setup(x => x.TurnGuardedAsync(It.IsAny<NpcGuardedTurnRequest>())).ThrowsAsync(exception);
    }

    private void WireTurn()
    {
        var classify = _lastClassify;
        var reply = _lastReply;
        _brain.Setup(x => x.TurnGuardedAsync(It.IsAny<NpcGuardedTurnRequest>()))
              .ReturnsAsync(() => new NpcGuardedTurnResult
                  {
                      Classify = classify is null
                          ? null
                          : new NpcGuardedClassifyResult
                          {
                              Classifications = classify.ToList(),
                              Provider = reply?.Provider ?? "luna@ultron",
                              Ms = 5
                          },
                      Reply = reply
                  }
              );
    }

    private static NpcGuardedReplyResult OkReply(string text, string factId, string mood = "neutral", string emote = null) =>
        new()
        {
            Ok = true,
            Text = text,
            Mood = mood,
            Emote = emote,
            DisclosedFactId = factId,
            VoiceId = "bm_george",
            Ms = 3
        };

    private static NpcGuardedClassification Tag(string tag, int? slot = null) =>
        new()
        {
            T = 1,
            Tag = tag,
            TopicSlot = slot,
            Ambiguous = false,
            Reason = "r",
            Evidence = "e"
        };

    private static DomainNpcSession MakeGuardedSession() =>
        new()
        {
            NpcId = "npc1",
            SessionId = "sess1",
            Mode = "dynamic",
            InteractionProfile = NpcInteractionProfiles.Guarded,
            VoiceId = "bm_george",
            Persona = new NpcPersona
            {
                Name = "Tomas",
                Role = "farmer",
                Language = "English",
                Mood = "wary",
                AttitudeToPlayers = "cautious"
            },
            Knowledge = "local farmer brief",
            Guarded = new NpcGuardedConfig
            {
                Concern = "retaliation against family",
                Facts =
                [
                    new NpcGuardedFact
                    {
                        Id = "f1",
                        Topic = "strange traffic",
                        Text = "Trucks have been rolling past the farm after dark."
                    },
                    new NpcGuardedFact
                    {
                        Id = "f2",
                        Topic = "where they stop",
                        Text = "They stop at the old mill by the river bend."
                    },
                    new NpcGuardedFact
                    {
                        Id = "f3",
                        Topic = "when they return",
                        Text = "They come back every third night near midnight."
                    }
                ]
            },
            GuardedState = new NpcGuardedState(),
            History = []
        };

    private static Dictionary<string, object> TurnData(string turnId = "turn7") =>
        new()
        {
            ["npcId"] = "npc1",
            ["sessionId"] = "sess1",
            ["turnId"] = turnId,
            ["gazeAddressed"] = "true",
            ["newTurns"] = new List<object>
            {
                new Dictionary<string, object>
                {
                    ["speakerId"] = "76561",
                    ["text"] = "seen any trucks around here?",
                    ["t"] = 1700000000000L
                }
            }
        };
}
