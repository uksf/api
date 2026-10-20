using System.Collections.Generic;
using System.Linq;
using UKSF.Api.ArmaServer.Npc.Models;

namespace UKSF.Api.ArmaServer.Npc.Services;

public static class NpcGuardedProfile
{
    public const string SafeDeflection = "I don't follow. Say that again plainly.";
    public const string WarnFallback = "Don't. Threaten my family again and this conversation ends.";
    public const string BackOffFallback = "Alright. Just don't go there again.";
    public const string BurnedFallback = "We're finished. I have nothing more to say to you.";
    public const string RefuseFallback = "I can't help you with that.";
    public const int ConcernGatedSlot = 3;

    public static string FallbackFor(string directive) =>
        directive switch
        {
            NpcGuardedDirectives.Warn    => WarnFallback,
            NpcGuardedDirectives.BackOff => BackOffFallback,
            NpcGuardedDirectives.Burned  => BurnedFallback,
            NpcGuardedDirectives.Refuse  => RefuseFallback,
            _                            => SafeDeflection
        };

    public static NpcGuardedEngineResult Evaluate(NpcGuardedState current, NpcGuardedConfig config, IReadOnlyList<NpcGuardedClassification> classifications)
    {
        var state = (current ?? new NpcGuardedState()).Clone();
        var ordered = (classifications ?? []).OrderBy(c => c.T).ThenBy(c => c.TopicSlot ?? 0).ToList();

        var sawNegative = false;
        var sawPositive = false;
        var becameBurned = false;
        var issuedWarning = false;
        var clearedWarning = false;
        var sawThreat = false;
        var sawAddressesConcern = false;
        var relevantSlots = new HashSet<int>();
        var anyActionable = false;

        foreach (var entry in ordered)
        {
            if (entry.Ambiguous || !NpcGuardedTags.IsKnown(entry.Tag)) continue;
            if (entry.AddressesConcern) sawAddressesConcern = true;
            if (state.Burned)
            {
                anyActionable = true;
                continue;
            }

            anyActionable = true;
            switch (entry.Tag)
            {
                case NpcGuardedTags.Threat:
                    sawThreat = true;
                    sawNegative = true;
                    if (state.PendingWarning)
                    {
                        state.Burned = true;
                        becameBurned = true;
                    }
                    else
                    {
                        state.PendingWarning = true;
                        issuedWarning = true;
                    }

                    break;

                case NpcGuardedTags.BackOff:
                    if (state.PendingWarning)
                    {
                        state.PendingWarning = false;
                        clearedWarning = true;
                    }

                    break;

                case NpcGuardedTags.Pressure: sawNegative = true; break;

                case NpcGuardedTags.RelevantQuestion:
                    sawPositive = true;
                    if (entry.TopicSlot is >= 1 and <= 3) relevantSlots.Add(entry.TopicSlot.Value);
                    break;

                case NpcGuardedTags.Rapport: sawPositive = true; break;

                case NpcGuardedTags.AddressesConcern: sawAddressesConcern = true; break;
            }
        }

        if (sawThreat)
        {
            state.ConcernAddressed = false;
            state.AskedFactId = null;
        }
        else if (sawAddressesConcern && !state.Burned)
        {
            state.ConcernAddressed = true;
        }

        var bandDelta = sawNegative ? -1 :
            sawPositive             ? 1 : 0;
        if (!state.Burned || becameBurned) state.CooperationBand = NpcCooperationBands.Step(state.CooperationBand, bandDelta);

        string permittedId = null;
        string permittedTopic = null;
        string permittedText = null;
        var heldForConcern = false;
        var facts = config?.Facts ?? [];

        if (!state.Burned && !sawThreat && anyActionable)
        {
            var nextIndex = NextFactIndex(facts, state.DisclosedFactIds);
            if (nextIndex >= 0 && nextIndex < facts.Count)
            {
                var slot = nextIndex + 1;
                var fact = facts[nextIndex];
                var topicOk = relevantSlots.Contains(slot) || (!string.IsNullOrEmpty(state.AskedFactId) && NpcGuardedFactIds.Same(state.AskedFactId, fact.Id));
                var concernOk = slot < ConcernGatedSlot || state.ConcernAddressed;
                if (topicOk && concernOk && !string.IsNullOrEmpty(fact.Id))
                {
                    permittedId = fact.Id;
                    permittedTopic = fact.Topic;
                    permittedText = fact.Text;
                }
                else if (topicOk && !concernOk)
                {
                    heldForConcern = true;
                    state.AskedFactId = fact.Id;
                }
            }
        }

        var directive = ResolveDirective(state, becameBurned, issuedWarning, clearedWarning, permittedId, anyActionable);
        return new NpcGuardedEngineResult
        {
            NextState = state,
            Directive = directive,
            PermittedFactId = permittedId,
            PermittedFactTopic = permittedTopic,
            PermittedFactText = permittedText,
            HeldForConcern = heldForConcern,
            Classifications = ordered
        };
    }

    private static string ResolveDirective(
        NpcGuardedState state,
        bool becameBurned,
        bool issuedWarning,
        bool clearedWarning,
        string permittedId,
        bool anyActionable
    )
    {
        if (becameBurned) return NpcGuardedDirectives.Burned;
        if (clearedWarning) return NpcGuardedDirectives.BackOff;
        if (issuedWarning) return NpcGuardedDirectives.Warn;
        if (state.Burned) return NpcGuardedDirectives.Refuse;
        if (!string.IsNullOrEmpty(permittedId)) return NpcGuardedDirectives.Disclose;
        if (!anyActionable) return NpcGuardedDirectives.Safe;
        return NpcGuardedDirectives.Normal;
    }

    public static NpcGuardedFact NextIncludableFact(NpcGuardedConfig config, NpcGuardedState state)
    {
        if (state is { Burned: true } or { PendingWarning: true }) return null;
        var facts = config?.Facts ?? [];
        var index = NextFactIndex(facts, state?.DisclosedFactIds);
        return index >= 0 && index < facts.Count ? facts[index] : null;
    }

    private static int NextFactIndex(IReadOnlyList<NpcGuardedFact> facts, IReadOnlyList<string> disclosed)
    {
        var have = new HashSet<string>((disclosed ?? []).Select(NpcGuardedFactIds.Normalise), StringComparer.Ordinal);
        for (var i = 0; i < facts.Count; i++)
        {
            if (!have.Contains(NpcGuardedFactIds.Normalise(facts[i].Id))) return i;
        }

        return -1;
    }
}
