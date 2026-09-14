using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.ArmaServer.Npc.Services;
using UKSF.Api.Core;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc;

public partial class NpcGuardedBrainServiceTests
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
    public async Task Classify_ValidBatch_PreservesOrderAndAddressesConcern()
    {
        _clacks.Setup(x => x.ChatAsync("npc", It.IsAny<string>(), It.IsAny<string>(), true, It.IsAny<int>(), 0d, It.IsAny<object>()))
               .ReturnsAsync(
                   new ClacksChatResult
                   {
                       Text =
                           """{"classifications":[{"t":10,"tag":"relevant_question","topicSlot":3,"addressesConcern":true,"ambiguous":false,"reason":"r","evidence":"return"},{"t":20,"tag":"other","topicSlot":null,"ambiguous":true,"reason":"x","evidence":""}]}""",
                       Model = "m",
                       Node = "n",
                       Ms = 12
                   }
               );

        var req = MakeClassifyReq(
            new NpcTurnDto
            {
                SpeakerId = "p",
                Text = "When do they return?",
                T = 10
            },
            new NpcTurnDto
            {
                SpeakerId = "p",
                Text = "uh",
                T = 20
            }
        );
        var result = await _sut.ClassifyGuardedAsync(req);
        result.Should().NotBeNull();
        result!.Classifications.Should().HaveCount(2);
        result.Classifications[0].Tag.Should().Be(NpcGuardedTags.RelevantQuestion);
        result.Classifications[0].AddressesConcern.Should().BeTrue();
        result.Classifications[0].TopicSlot.Should().Be(3);
        result.Classifications[1].Ambiguous.Should().BeTrue();
    }

    [Fact]
    public async Task Classify_UnknownTag_BecomesOther()
    {
        _clacks.Setup(x => x.ChatAsync("npc", It.IsAny<string>(), It.IsAny<string>(), true, It.IsAny<int>(), 0d, It.IsAny<object>()))
               .ReturnsAsync(
                   new ClacksChatResult
                   {
                       Text = """{"classifications":[{"t":1,"tag":"nope","ambiguous":false,"reason":"x","evidence":"hello"}]}""",
                       Model = "m",
                       Node = "n"
                   }
               );

        var result = await _sut.ClassifyGuardedAsync(
            MakeClassifyReq(
                new NpcTurnDto
                {
                    SpeakerId = "p",
                    Text = "hello",
                    T = 1
                }
            )
        );
        result.Should().NotBeNull();
        result!.Classifications.Should().ContainSingle();
        result.Classifications[0].Tag.Should().Be(NpcGuardedTags.Other);
    }

    [Fact]
    public async Task Classify_ExtraEntry_ReturnsNull()
    {
        _clacks.Setup(x => x.ChatAsync("npc", It.IsAny<string>(), It.IsAny<string>(), true, It.IsAny<int>(), 0d, It.IsAny<object>()))
               .ReturnsAsync(
                   new ClacksChatResult
                   {
                       Text =
                           """{"classifications":[{"t":1,"tag":"threat","ambiguous":false,"reason":"a","evidence":"hurt"},{"t":1,"tag":"threat","ambiguous":false,"reason":"b","evidence":"hurt"}]}""",
                       Model = "m",
                       Node = "n"
                   }
               );

        (await _sut.ClassifyGuardedAsync(
                MakeClassifyReq(
                    new NpcTurnDto
                    {
                        SpeakerId = "p",
                        Text = "I hurt them",
                        T = 1
                    }
                )
            )).Should()
              .BeNull();
    }

    [Fact]
    public async Task Classify_EvidenceNotInUtterance_MarksAmbiguous()
    {
        _clacks.Setup(x => x.ChatAsync("npc", It.IsAny<string>(), It.IsAny<string>(), true, It.IsAny<int>(), 0d, It.IsAny<object>()))
               .ReturnsAsync(
                   new ClacksChatResult
                   {
                       Text = """{"classifications":[{"t":1,"tag":"threat","ambiguous":false,"reason":"a","evidence":"explode"}]}""",
                       Model = "m",
                       Node = "n"
                   }
               );

        var result = await _sut.ClassifyGuardedAsync(
            MakeClassifyReq(
                new NpcTurnDto
                {
                    SpeakerId = "p",
                    Text = "I hurt them",
                    T = 1
                }
            )
        );
        result.Should().NotBeNull();
        result!.Classifications.Should().ContainSingle();
        result.Classifications[0].Tag.Should().Be(NpcGuardedTags.Threat);
        result.Classifications[0].Ambiguous.Should().BeTrue();
    }

    [Fact]
    public async Task Classify_TimestampMismatch_ReturnsNull()
    {
        _clacks.Setup(x => x.ChatAsync("npc", It.IsAny<string>(), It.IsAny<string>(), true, It.IsAny<int>(), 0d, It.IsAny<object>()))
               .ReturnsAsync(
                   new ClacksChatResult
                   {
                       Text = """{"classifications":[{"t":99,"tag":"other","ambiguous":true,"reason":"a","evidence":""}]}""",
                       Model = "m",
                       Node = "n"
                   }
               );

        (await _sut.ClassifyGuardedAsync(
                MakeClassifyReq(
                    new NpcTurnDto
                    {
                        SpeakerId = "p",
                        Text = "hello",
                        T = 1
                    }
                )
            )).Should()
              .BeNull();
    }

    [Fact]
    public async Task Classify_MalformedJson_ReturnsNull()
    {
        _clacks.Setup(x => x.ChatAsync("npc", It.IsAny<string>(), It.IsAny<string>(), true, It.IsAny<int>(), 0d, It.IsAny<object>()))
               .ReturnsAsync(
                   new ClacksChatResult
                   {
                       Text = "not-json",
                       Model = "m",
                       Node = "n"
                   }
               );

        (await _sut.ClassifyGuardedAsync(MakeClassifyReq())).Should().BeNull();
    }

    [Fact]
    public async Task Reply_ParsesValidJson()
    {
        _clacks.Setup(x => x.ChatAsync("npc", It.IsAny<string>(), It.IsAny<string>(), true, It.IsAny<int>(), It.IsAny<double>(), It.IsAny<object>()))
               .ReturnsAsync(
                   new ClacksChatResult
                   {
                       Text = """{"text":"Keep your voice down.","mood":"afraid","emote":"glances away","disclosedFactId":"f1"}""",
                       Model = "m",
                       Node = "n",
                       Ms = 9
                   }
               );

        var result = await _sut.ReplyGuardedAsync(MakeReplyReq());
        result.Ok.Should().BeTrue();
        result.Text.Should().Be("Keep your voice down.");
        result.Mood.Should().Be("afraid");
        result.Emote.Should().Be("glances away");
        result.DisclosedFactId.Should().Be("f1");
        result.VoiceId.Should().Be("bm_george");
    }

    [Fact]
    public async Task Reply_MalformedJson_ReturnsFailure_NoThrow()
    {
        _clacks.Setup(x => x.ChatAsync("npc", It.IsAny<string>(), It.IsAny<string>(), true, It.IsAny<int>(), It.IsAny<double>(), It.IsAny<object>()))
               .ReturnsAsync(
                   new ClacksChatResult
                   {
                       Text = "{bad",
                       Model = "m",
                       Node = "n"
                   }
               );

        var result = await _sut.ReplyGuardedAsync(MakeReplyReq());
        result.Ok.Should().BeFalse();
        result.Failure.Should().Contain("parse");
    }

    [Fact]
    public async Task Reply_NullClacks_ReturnsFailure()
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

        var result = await _sut.ReplyGuardedAsync(MakeReplyReq());
        result.Ok.Should().BeFalse();
    }

    [Fact]
    public async Task ClassifyPrompt_UsesForcedJsonAndZeroTemperature()
    {
        string system = null;
        _clacks.Setup(x => x.ChatAsync("npc", It.IsAny<string>(), It.IsAny<string>(), true, It.IsAny<int>(), 0d, It.IsAny<object>()))
               .Callback<string, string, string, bool, int, double, object>((_, s, _, _, _, _, _) => system = s)
               .ReturnsAsync(
                   new ClacksChatResult
                   {
                       Text = """{"classifications":[{"t":1,"tag":"other","ambiguous":true,"reason":"n","evidence":""}]}""",
                       Model = "m",
                       Node = "n"
                   }
               );

        await _sut.ClassifyGuardedAsync(MakeClassifyReq());
        system.Should().Contain("Topic cues");
        system.Should().Contain("addressesConcern");
        system.Should().Contain("Exactly one");
        foreach (var fact in new[] { "Trucks have been rolling", "old mill", "third night" }) system.Should().NotContain(fact);
    }
}
