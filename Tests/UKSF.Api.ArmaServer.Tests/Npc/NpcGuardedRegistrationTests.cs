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

public class NpcGuardedRegistrationTests
{
    private readonly Mock<INpcSessionsContext> _sessions = new();
    private readonly Mock<INpcAudioClipsContext> _clips = new();
    private readonly Mock<INpcBrainClient> _brain = new();
    private readonly Mock<IClacksClient> _clacks = new();
    private readonly Mock<IGameServerCommandSender> _commands = new();
    private readonly Mock<INpcAudioStore> _audio = new();
    private readonly Mock<INpcVoiceStore> _voices = new();
    private readonly Mock<INpcVoicesContext> _voiceCtx = new();
    private readonly Mock<IVariablesService> _vars = new();
    private readonly Mock<IUksfLogger> _logger = new();
    private readonly NpcBrokerService _sut;
    private DomainNpcSession _stored;

    public NpcGuardedRegistrationTests()
    {
        _vars.Setup(x => x.GetFeatureState("NPC_BROKER")).Returns(true);
        _sessions.Setup(x => x.GetSingle(It.IsAny<Func<DomainNpcSession, bool>>())).Returns(() => _stored);
        _sessions.Setup(x => x.Add(It.IsAny<DomainNpcSession>())).Callback<DomainNpcSession>(s => _stored = s).Returns(Task.CompletedTask);
        _sessions.Setup(x => x.Replace(It.IsAny<DomainNpcSession>())).Callback<DomainNpcSession>(s => _stored = s).Returns(Task.CompletedTask);
        _sessions.Setup(x => x.Update(It.IsAny<Expression<Func<DomainNpcSession, bool>>>(), It.IsAny<UpdateDefinition<DomainNpcSession>>()))
                 .Returns(Task.CompletedTask);
        _clacks.Setup(x => x.WarmAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<int>())).ReturnsAsync(true);
        _voices.Setup(x => x.ReadAsync(It.IsAny<string>())).ReturnsAsync(new byte[] { 1, 2, 3, 4 });
        _commands.Setup(x => x.SendCommandAsync(It.IsAny<int>(), It.IsAny<string>())).Returns(Task.CompletedTask);

        _sut = new NpcBrokerService(
            _sessions.Object,
            _clips.Object,
            _brain.Object,
            _clacks.Object,
            _commands.Object,
            _audio.Object,
            _voices.Object,
            _voiceCtx.Object,
            _vars.Object,
            _logger.Object
        );
    }

    [Fact]
    public async Task MissingProfile_RegistersAsConversation()
    {
        await _sut.HandleRegisterAsync(5006, BaseData());
        _stored.InteractionProfile.Should().Be(NpcInteractionProfiles.Conversation);
        _stored.Guarded.Should().BeNull();
        _stored.GuardedState.Should().BeNull();
        _clacks.Verify(x => x.WarmAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task ValidGuarded_StoresConfigAndDefaultState_WithoutFactsInKnowledge()
    {
        await _sut.HandleRegisterAsync(5006, GuardedData());
        _stored.InteractionProfile.Should().Be(NpcInteractionProfiles.Guarded);
        _stored.Guarded.Concern.Should().Be("retaliation against family");
        _stored.Guarded.Facts.Should().HaveCount(3);
        _stored.GuardedState.CooperationBand.Should().Be(NpcCooperationBands.Guarded);
        _stored.GuardedState.DisclosedFactIds.Should().BeEmpty();
        _stored.Knowledge.Should().NotContain("Trucks have been rolling");
        _stored.Knowledge.Should().Be("local farmer brief");
    }

    [Fact]
    public void LegacyDocument_MissingProfile_DeserialisesAsConversation()
    {
        var session = new DomainNpcSession { NpcId = "n", SessionId = "s" };
        session.InteractionProfile.Should().Be(NpcInteractionProfiles.Conversation);
        session.Guarded.Should().BeNull();
        session.GuardedState.Should().BeNull();
    }

    [Fact]
    public async Task GuardedPlusScripted_RejectsBeforeWarm()
    {
        var data = GuardedData();
        data["mode"] = "scripted";
        await _sut.HandleRegisterAsync(5006, data);
        _stored.Should().BeNull();
        _clacks.Verify(x => x.WarmAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<int>()), Times.Never);
        _logger.Verify(x => x.LogWarning(It.Is<string>(m => m.Contains("guarded validation failed"))), Times.Once);
    }

    [Fact]
    public async Task MissingConcern_RejectsBeforeWarm()
    {
        var data = GuardedData();
        ((Dictionary<string, object>)data["guarded"])["concern"] = "";
        await _sut.HandleRegisterAsync(5006, data);
        _stored.Should().BeNull();
        _clacks.Verify(x => x.WarmAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task FactTextInKnowledge_RejectsBeforeWarm()
    {
        var data = GuardedData();
        data["knowledge"] = "Trucks have been rolling past the farm after dark.";
        await _sut.HandleRegisterAsync(5006, data);
        _stored.Should().BeNull();
        _clacks.Verify(x => x.WarmAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task DuplicateFactIds_Rejects()
    {
        var data = GuardedData();
        var facts = (List<object>)((Dictionary<string, object>)data["guarded"])["facts"];
        ((Dictionary<string, object>)facts[1])["id"] = "f1";
        await _sut.HandleRegisterAsync(5006, data);
        _stored.Should().BeNull();
    }

    [Fact]
    public async Task DuplicateUnchanged_PreservesStateHistory()
    {
        await _sut.HandleRegisterAsync(5006, GuardedData());
        _stored.GuardedState.PendingWarning = true;
        _stored.GuardedState.DisclosedFactIds = ["f1"];
        _stored.History =
        [
            new NpcHistoryEntry
            {
                Role = "player",
                Text = "hi",
                T = 1
            }
        ];
        var historyCount = _stored.History.Count;

        await _sut.HandleRegisterAsync(5006, GuardedData());

        _stored.GuardedState.PendingWarning.Should().BeTrue();
        _stored.GuardedState.DisclosedFactIds.Should().Equal("f1");
        _stored.History.Should().HaveCount(historyCount);
    }

    [Fact]
    public async Task DuplicateChangedContent_RejectsWithoutReset()
    {
        await _sut.HandleRegisterAsync(5006, GuardedData());
        _stored.GuardedState.Burned = true;
        var data = GuardedData();
        ((Dictionary<string, object>)data["guarded"])["concern"] = "different concern";

        await _sut.HandleRegisterAsync(5006, data);

        _stored.GuardedState.Burned.Should().BeTrue();
        _stored.Guarded.Concern.Should().Be("retaliation against family");
        _logger.Verify(x => x.LogWarning(It.Is<string>(m => m.Contains("without resetGuarded"))), Times.Once);
    }

    [Fact]
    public async Task ExplicitReset_AcceptsChangedContent_AndClearsState()
    {
        await _sut.HandleRegisterAsync(5006, GuardedData());
        _stored.GuardedState.Burned = true;
        _stored.History =
        [
            new NpcHistoryEntry
            {
                Role = "npc",
                Text = "old",
                T = 1
            }
        ];

        var data = GuardedData();
        ((Dictionary<string, object>)data["guarded"])["concern"] = "new concern text";
        data["resetGuarded"] = true;

        await _sut.HandleRegisterAsync(5006, data);

        _stored.Guarded.Concern.Should().Be("new concern text");
        _stored.GuardedState.Burned.Should().BeFalse();
        _stored.GuardedState.DisclosedFactIds.Should().BeEmpty();
        _stored.History.Should().BeEmpty();
    }

    private static Dictionary<string, object> BaseData() =>
        new()
        {
            ["npcId"] = "npc1",
            ["sessionId"] = "sess1",
            ["knowledge"] = "local farmer brief",
            ["voiceId"] = "bm_george",
            ["mode"] = "dynamic",
            ["persona"] = new Dictionary<string, object>
            {
                ["name"] = "Tomas",
                ["role"] = "farmer",
                ["language"] = "English",
                ["mood"] = "wary",
                ["attitudeToPlayers"] = "cautious"
            },
            ["scripted"] = new Dictionary<string, object> { ["lines"] = new List<object>(), ["deflection"] = "no" }
        };

    private static Dictionary<string, object> GuardedData()
    {
        var data = BaseData();
        data["interactionProfile"] = "guarded";
        data["guarded"] = new Dictionary<string, object>
        {
            ["concern"] = "retaliation against family",
            ["facts"] = new List<object>
            {
                Fact("f1", "strange traffic", "Trucks have been rolling past the farm after dark."),
                Fact("f2", "where they stop", "They stop at the old mill by the river bend."),
                Fact("f3", "when they return", "They come back every third night near midnight.")
            }
        };
        return data;
    }

    private static Dictionary<string, object> Fact(string id, string topic, string text) =>
        new()
        {
            ["id"] = id,
            ["topic"] = topic,
            ["text"] = text
        };
}
