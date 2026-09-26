using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using UKSF.Api.ArmaServer.Npc.Services;
using UKSF.Api.ArmaServer.Npc.Models;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc;

public partial class NpcBrokerServiceTests
{
    private void TwoNpcsInTheRoom()
    {
        var asad = MakeDynamicSession();
        var karim = MakeDynamicSession("npc2");
        karim.Persona = new NpcPersona { Name = "Karim", Role = "trader", Language = "Arabic", Mood = "calm", AttitudeToPlayers = "neutral" };
        _sessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainNpcSession, bool>>())).Returns(asad);
        _sessionsContext.Setup(x => x.Get(It.IsAny<Func<DomainNpcSession, bool>>())).Returns([asad, karim]);
        _brainClient.Setup(x => x.RespondAsync(It.IsAny<RespondRequest>())).ReturnsAsync(new RespondResult { Text = "what", Mood = "neutral", VoiceId = "bm_george" });
        _clacks.Setup(x => x.SpeakStreamAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<string, Task>>()))
               .Returns(async (string _, string _, string _, Func<string, Task> onFrame) => await onFrame("QQ=="));
    }

    private static Dictionary<string, object> Says(string text, bool lookedAt) =>
        new()
        {
            ["npcId"] = "npc1",
            ["sessionId"] = "session1",
            ["turnId"] = "turn7",
            ["gazeAddressed"] = lookedAt,
            ["newTurns"] = new List<object> { new Dictionary<string, object> { ["speakerId"] = "76561", ["text"] = text, ["t"] = 1700000000000L } }
        };

    [Fact]
    public async Task Address_NameUsedAsATopic_JevHandsTheTurnToTheNpcLookedAt()
    {
        TwoNpcsInTheRoom();
        _brainClient.Setup(x => x.IsAddressedAsync(It.IsAny<IReadOnlyList<string>>(), "Asad", true, It.IsAny<string>(), "npc1")).ReturnsAsync(true);

        await _sut.HandleTurnAsync(5006, Says("Karim says you know where the ammo is. Do you?", true));

        _brainClient.Verify(x => x.RespondAsync(It.IsAny<RespondRequest>()), Times.Once);
    }

    [Fact]
    public async Task Address_JevSaysAnotherNpcWasCalled_LookedAtNpcStaysSilent()
    {
        TwoNpcsInTheRoom();
        _brainClient.Setup(x => x.IsAddressedAsync(It.IsAny<IReadOnlyList<string>>(), "Asad", true, It.IsAny<string>(), "npc1")).ReturnsAsync(false);

        await _sut.HandleTurnAsync(5006, Says("Kareem, where is the ammo?", true));

        _brainClient.Verify(x => x.RespondAsync(It.IsAny<RespondRequest>()), Times.Never);
    }

    [Fact]
    public async Task Address_NoNameInTheWords_JevIsNotAsked()
    {
        TwoNpcsInTheRoom();

        await _sut.HandleTurnAsync(5006, Says("where is the ammo?", true));

        _brainClient.Verify(x => x.IsAddressedAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _brainClient.Verify(x => x.RespondAsync(It.IsAny<RespondRequest>()), Times.Once);
    }

    [Fact]
    public async Task Address_JevGivesNoAnswer_MatcherAndGazeDecide()
    {
        TwoNpcsInTheRoom();

        await _sut.HandleTurnAsync(5006, Says("Karim, where is the ammo?", true));

        _brainClient.Verify(x => x.RespondAsync(It.IsAny<RespondRequest>()), Times.Never);
    }
}
