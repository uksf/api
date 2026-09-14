using System;
using System.Collections.Generic;
using System.Linq;

namespace UKSF.Api.ArmaServer.Npc.Services;

/// Decides which NPC, if any, an utterance addresses by name.
///
/// The transcript comes from speech recognition, and many of the players are not native
/// English speakers, so names arrive mangled: "Mel" for "Merl", "Merle", "Mole". The
/// match therefore tolerates misspellings, but only so far: a loose match that lets
/// "Marl" reach "Merl" while Merl and Marl stand together is worse than a miss, because a
/// miss just leaves silence the player retries against, while a false hit makes the wrong
/// man answer. Unambiguous matches only — if two NPCs both plausibly match, the utterance
/// is treated as unnamed and the gaze gate decides.
///
/// This must stay cheap: it runs per turn, before the brain, and adds no latency beyond a
/// handful of string comparisons. Borderline cases are not resolved here at all; they are
/// flagged so the brain can decide, which is the one place an LLM call is acceptable.
public static class NpcNameMatcher
{
    public enum Match
    {
        None, // no name in the text — let the gaze gate decide
        This, // clearly this NPC
        Other, // clearly a different NPC
        Borderline // close but unclear — worth letting the brain decide
    }

    /// Classify one utterance against this NPC's name and every other name in the space.
    public static Match Classify(string text, string ownName, IReadOnlyCollection<string> allNames)
    {
        if (string.IsNullOrWhiteSpace(text)) return Match.None;

        var tokens = Tokenise(text);
        if (tokens.Count == 0) return Match.None;

        var own = Normalise(ownName);
        if (string.IsNullOrEmpty(own)) return Match.None;

        var ownScore = BestScore(tokens, own);
        var otherScores = allNames.Where(n => !string.Equals(n, ownName, StringComparison.OrdinalIgnoreCase))
                                  .Select(n => BestScore(tokens, Normalise(n)))
                                  .ToList();
        var otherBest = otherScores.Count > 0 ? otherScores.Max() : 0;

        // Neither side shows any signal: unnamed.
        if (ownScore == 0 && otherBest == 0) return Match.None;

        // One side reaches solid and the other does not.
        if (ownScore >= 2 && otherBest < 2) return Match.This;
        if (otherBest >= 2 && ownScore < 2) return Match.Other;

        // A loose hit that only one side shows still resolves. STT through an accent can
        // cost two edits on a short name ("Parval" for Pavel), and treating that as unnamed
        // silently hands the turn to whoever is being looked at instead.
        if (ownScore >= 1 && otherBest == 0) return Match.This;
        if (otherBest >= 1 && ownScore == 0) return Match.Other;

        // Anything else with any signal at all is close enough to be worth a second opinion.
        return Match.Borderline;
    }

    /// 0 = no match, 1 = loose (fuzzy/phonetic), 2 = solid (exact or one edit on a short name).
    private static int BestScore(IReadOnlyList<string> tokens, string name)
    {
        if (string.IsNullOrEmpty(name)) return 0;

        var best = 0;
        foreach (var raw in tokens)
        {
            var token = Normalise(raw);
            if (token.Length == 0) continue;

            if (token == name) return 2;

            var distance = Levenshtein(token, name);
            var allowed = name.Length <= 5 ? 1 : 2; // short names forgive less
            if (distance == 0) return 2; // exact after normalisation (confusables)
            if (distance <= allowed)
            {
                best = Math.Max(best, distance <= 1 && name.Length >= 4 ? 2 : 1);
            }
            else if (distance <= allowed + 1 || SoundsAlike(token, name))
            {
                // Two edits out, or near enough once vowels and confusables are stripped.
                best = Math.Max(best, 1);
            }
        }

        return best;
    }

    /// Coarse phonetic match for the accent-driven cases edit distance misses: collapse
    /// vowels and confusable consonants, then allow a small edit distance on the skeletons.
    private static bool SoundsAlike(string a, string b)
    {
        var sa = Skeleton(a);
        var sb = Skeleton(b);
        if (sa.Length < 3 || sb.Length < 3) return false;

        return Levenshtein(sa, sb) <= Math.Max(1, sb.Length / 4);
    }

    private static string Skeleton(string s)
    {
        var chars = s.Select(c => c switch
                         {
                             'a' or 'e' or 'i' or 'o' or 'u' or 'y' => '\0',
                             'b' or 'p'                             => 'b',
                             'c' or 'k' or 'q'                      => 'k',
                             'd' or 't'                             => 't',
                             'f' or 'v' or 'w'                      => 'f',
                             'g' or 'j'                             => 'g',
                             'l' or 'r'                             => 'l',
                             'm' or 'n'                             => 'm',
                             's' or 'z'                             => 's',
                             _                                      => c
                         }
                     )
                     .Where(c => c != '\0');
        return string.Concat(chars);
    }

    /// Lowercase, strip non-letters, map the confusables STT and accents produce.
    private static string Normalise(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;

        var lower = new string(s.ToLowerInvariant().Where(char.IsLetter).ToArray());
        return lower.Replace("ph", "f").Replace("th", "t").Replace("ck", "k").Replace("qu", "k");
    }

    private static List<string> Tokenise(string text)
    {
        var tokens = new List<string>();
        var current = new List<char>();
        foreach (var c in text)
        {
            if (char.IsLetter(c))
            {
                current.Add(c);
            }
            else if (current.Count > 0)
            {
                tokens.Add(new string(current.ToArray()));
                current.Clear();
            }
        }

        if (current.Count > 0) tokens.Add(new string(current.ToArray()));
        return tokens;
    }

    private static int Levenshtein(string a, string b)
    {
        if (a == b) return 0;
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        var prev = new int[b.Length + 1];
        var curr = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) prev[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            curr[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(Math.Min(curr[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
            }

            (prev, curr) = (curr, prev);
        }

        return prev[b.Length];
    }
}
