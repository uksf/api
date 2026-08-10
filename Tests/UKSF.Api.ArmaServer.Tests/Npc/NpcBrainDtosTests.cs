using System.Text.Json;
using FluentAssertions;
using UKSF.Api.ArmaServer.Npc.Models;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc;

public class NpcBrainDtosTests
{
    [Fact]
    public void RespondRequest_SerialisesToVmCamelCaseContract()
    {
        var request = new RespondRequest
        {
            NpcId = "npc1",
            Persona = new NpcPersona
            {
                Name = "Asad",
                Role = "guard",
                Language = "Arabic",
                Mood = "on edge",
                AttitudeToPlayers = "hostile"
            },
            Knowledge = "knows the ammo cache location",
            Mode = "dynamic",
            VoiceId = "bm_george",
            History =
            [
                new NpcHistoryEntry
                {
                    Role = "npc",
                    Speaker = "",
                    Text = "leave now",
                    T = 1699999999000
                }
            ],
            NewTurns =
            [
                new NpcTurnDto
                {
                    SpeakerId = "76561",
                    Text = "where is the ammo?",
                    T = 1700000000000
                }
            ]
        };

        var json = JsonSerializer.Serialize(request, NpcBrainJson.Options);

        json.Should().Contain("\"npcId\":\"npc1\"");
        json.Should().Contain("\"attitudeToPlayers\":\"hostile\"");
        json.Should().Contain("\"history\":[");
        json.Should().Contain("\"role\":\"npc\"");
        json.Should().Contain("\"newTurns\":[");
        json.Should().Contain("\"speakerId\":\"76561\"");
        json.Should().Contain("\"t\":1700000000000");
        json.Should().NotContain("scripted"); // null + ignore-null
    }

    [Fact]
    public void RespondResult_DeserialisesVmResponse()
    {
        const string vmJson = """{"text":"get lost","lineId":null,"audioBase64":"AAAA","durationMs":1200,"provider":"claude"}""";
        var result = JsonSerializer.Deserialize<RespondResult>(vmJson, NpcBrainJson.Options);
        result.Should().NotBeNull();
        result!.Text.Should().Be("get lost");
        result.LineId.Should().BeNull();
        result.AudioBase64.Should().Be("AAAA");
        result.DurationMs.Should().Be(1200);
    }

    [Fact]
    public void PrerenderResult_DeserialisesItems()
    {
        const string vmJson = """{"items":[{"id":"f0","audioBase64":"QQ==","durationMs":600}]}""";
        var result = JsonSerializer.Deserialize<PrerenderResult>(vmJson, NpcBrainJson.Options);
        result!.Items.Should().HaveCount(1);
        result.Items[0].Id.Should().Be("f0");
        result.Items[0].DurationMs.Should().Be(600);
    }

    [Fact]
    public void DomainNpcSession_MissingGuardedFields_DefaultToConversation()
    {
        const string json = """{"npcId":"n1","sessionId":"s1","mode":"dynamic","knowledge":"k"}""";
        var session = JsonSerializer.Deserialize<DomainNpcSession>(json, NpcBrainJson.Options);
        session!.InteractionProfile.Should().Be(NpcInteractionProfiles.Conversation);
        session.Guarded.Should().BeNull();
        session.GuardedState.Should().BeNull();
    }

    [Fact]
    public void GuardedReplyModelOutput_DeserialisesForcedJson()
    {
        const string json = """{"text":"stay back","mood":"afraid","emote":"steps away","disclosedFactId":"f1"}""";
        var output = JsonSerializer.Deserialize<NpcGuardedReplyModelOutput>(json, NpcBrainJson.Options);
        output!.Text.Should().Be("stay back");
        output.Mood.Should().Be("afraid");
        output.DisclosedFactId.Should().Be("f1");
    }
}
