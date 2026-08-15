using System;
using System.Linq;
using System.Threading.Tasks;
using UKSF.Api.ArmaServer.Npc.Models;
using static UKSF.Api.ArmaServer.Converters.PersistenceConversionHelpers;

namespace UKSF.Api.ArmaServer.Npc.Services;

// Addressing half of the broker: which NPC an utterance was meant for.
//
// Every talkable NPC in earshot receives every utterance — speech is not directional, and
// only sending to the one being looked at meant naming an NPC you were not facing reached
// nobody. So the decision lives here: a named NPC answers, and when no name is used the
// one being looked at answers. Anything else stays silent and tells the game to stop its
// filler loop, or the player waits out a chorus of noises for a reply nobody will give.
public partial class NpcBrokerService
{
    private enum AddressDecision
    {
        Answer,
        StaySilent,
        AskTheBrain // borderline name match; the brain may decline with [none]
    }

    private AddressDecision DecideAddress(DomainNpcSession session, string sessionId, string latestText, bool gazeAddressed)
    {
        var allNames = sessionsContext.Get(x => x.SessionId == sessionId)
                                      .Select(s => s.Persona?.Name ?? string.Empty)
                                      .Where(n => !string.IsNullOrEmpty(n))
                                      .ToList();

        var match = NpcNameMatcher.Classify(latestText, session.Persona?.Name ?? string.Empty, allNames);
        return match switch
        {
            NpcNameMatcher.Match.Other      => AddressDecision.StaySilent,
            NpcNameMatcher.Match.This       => AddressDecision.Answer,
            NpcNameMatcher.Match.Borderline => AddressDecision.AskTheBrain,
            _                               => gazeAddressed ? AddressDecision.Answer : AddressDecision.StaySilent
        };
    }

    private static bool ParseGazeAddressed(object raw) =>
        raw switch
        {
            null     => false,
            bool b   => b,
            string s => s.Equals("true", StringComparison.OrdinalIgnoreCase) || s == "1",
            _        => ToBool(raw)
        };

    private async Task CancelTurnAsync(int apiPort, string npcId, string reason)
    {
        logger.LogInfo($"npc_turn: '{npcId}' stays silent ({reason})");
        await commandSender.SendCommandAsync(apiPort, NpcAudioEnvelopeBuilder.BuildTurnCancel(npcId));
        await SendDebugStateAsync(apiPort, npcId, "", "stay_silent");
    }
}
