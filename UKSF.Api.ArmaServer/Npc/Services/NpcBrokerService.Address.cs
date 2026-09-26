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
    internal enum AddressDecision
    {
        Answer,
        StaySilent,
        AskTheBrain // borderline name match; the brain may decline with [none]
    }

    /// A line with any name hit in it goes to Jev: a misheard name ("Parval") or a name used as a
    /// topic ("Pavel says...") fools the matcher. With no name, or no answer from Jev, gaze and
    /// the matcher decide.
    private async Task<AddressDecision> DecideAddressAsync(DomainNpcSession session, string sessionId, string latestText, bool gazeAddressed)
    {
        var allNames = sessionsContext.Get(x => x.SessionId == sessionId)
                                      .Select(s => s.Persona?.Name ?? string.Empty)
                                      .Where(n => !string.IsNullOrEmpty(n))
                                      .ToList();

        var match = NpcNameMatcher.Classify(latestText, session.Persona?.Name ?? string.Empty, allNames);
        var decision = DecideAddress(match, gazeAddressed);
        if (match != NpcNameMatcher.Match.None)
        {
            var addressed = await brainClient.IsAddressedAsync(allNames, session.Persona?.Name ?? string.Empty, gazeAddressed, latestText, session.NpcId);
            if (addressed is { } yes)
            {
                logger.LogInfo($"npc_turn: '{session.NpcId}' jev address={yes} (matcher said {decision})");
                decision = yes ? AddressDecision.Answer : AddressDecision.StaySilent;
            }
        }

        var preview = latestText.Length <= 80 ? latestText : latestText[..80];
        logger.LogInfo($"npc_turn: '{session.NpcId}' name='{session.Persona?.Name}' match={match} gaze={gazeAddressed} decision={decision} text='{preview}'");
        return decision;
    }

    /// Gaze owns the turn unless the player clearly named someone else.
    /// A non-gazed NPC answers only on a solid name hit — not a loose
    /// phonetic slip, and not AskTheBrain. Conversation + AskTheBrain was
    /// letting Pavel reply while Tomas (the look target) was still waiting.
    internal static AddressDecision DecideAddress(NpcNameMatcher.Match match, bool gazeAddressed)
    {
        if (gazeAddressed)
        {
            return match == NpcNameMatcher.Match.Other ? AddressDecision.StaySilent : AddressDecision.Answer;
        }

        return match == NpcNameMatcher.Match.This ? AddressDecision.Answer : AddressDecision.StaySilent;
    }

    private static bool ParseGazeAddressed(object raw) =>
        raw switch
        {
            null     => false,
            bool b   => b,
            string s => s.Equals("true", StringComparison.OrdinalIgnoreCase) || s == "1",
            _        => ToBool(raw)
        };

    private async Task CancelTurnAsync(int apiPort, string npcId, string turnId, string reason)
    {
        logger.LogInfo($"npc_turn: '{npcId}' stays silent ({reason})");
        await commandSender.SendCommandAsync(apiPort, NpcAudioEnvelopeBuilder.BuildTurnCancel(npcId, turnId));
        await SendDebugStateAsync(apiPort, npcId, "", "stay_silent");
    }
}
