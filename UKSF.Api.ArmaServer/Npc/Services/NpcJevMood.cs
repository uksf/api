using System.Collections.Generic;
using System.Linq;

namespace UKSF.Api.ArmaServer.Npc.Services;

/// The Jev mood question every NPC turn asks. Jev reads criteria literally, so each names the
/// situation that earns it. The TTS styling for each mood lives in MoodScripts.Table.
public static class NpcJevMood
{
    private static readonly Dictionary<string, string> Criteria = new()
    {
        [MoodScripts.Neutral] = "The default: ordinary talk, questions, small talk, thanks, or not understanding what was said.",
        ["afraid"] = "The words threaten the character, their family or their goods, or put them in danger right now.",
        ["angry"] = "The words insult, bully or pressure the character, and they push back.",
        ["sad"] = "The words are about loss, grief or hardship.",
        ["happy"] = "The character is plainly pleased or relieved by the words, such as good news or real help."
    };

    // "Not their general worries": without it a concern or disposition line alone tips thanks and
    // small talk to afraid, because Jev answers every question against one shared state.
    public static JevQuestion Question(string name) =>
        JevQuestion.Choice(
            $"Judge the current words, and any threat earlier in the conversation shown that has not been truly apologised for, not {name}'s general worries. Which mood fits {name}'s reply? Choose neutral unless the words clearly meet another mood's description.",
            Criteria.Where(kv => MoodScripts.IsValid(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value)
        );
}
