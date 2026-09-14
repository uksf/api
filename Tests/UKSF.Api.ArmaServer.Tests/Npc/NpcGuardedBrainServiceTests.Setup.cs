using System.Collections.Generic;
using System.Linq;
using UKSF.Api.ArmaServer.Npc.Models;

namespace UKSF.Api.ArmaServer.Tests.Npc;

public partial class NpcGuardedBrainServiceTests
{
    private static NpcGuardedClassifyRequest MakeClassifyReq(params NpcTurnDto[] utterances) =>
        new()
        {
            NpcId = "n1",
            Persona = new NpcPersona
            {
                Name = "Tomas",
                Role = "farmer",
                Language = "English",
                Mood = "wary",
                AttitudeToPlayers = "cautious"
            },
            Concern = "retaliation",
            TopicCues = [("f1", "traffic"), ("f2", "stop"), ("f3", "return")],
            State = new NpcGuardedState(),
            Utterances = utterances is { Length: > 0 }
                ? utterances.ToList()
                :
                [
                    new NpcTurnDto
                    {
                        SpeakerId = "p",
                        Text = "hello",
                        T = 1
                    }
                ]
        };

    private static NpcGuardedReplyRequest MakeReplyReq() =>
        new()
        {
            NpcId = "n1",
            Persona = new NpcPersona
            {
                Name = "Tomas",
                Role = "farmer",
                Language = "English",
                Mood = "wary",
                AttitudeToPlayers = "cautious"
            },
            Knowledge = "brief",
            History = [],
            NewTurns =
            [
                new NpcTurnDto
                {
                    SpeakerId = "p",
                    Text = "hi",
                    T = 1
                }
            ],
            Directive = NpcGuardedDirectives.Disclose,
            PermittedFactId = "f1",
            PermittedFactTopic = "traffic",
            VoiceId = "bm_george"
        };
}
