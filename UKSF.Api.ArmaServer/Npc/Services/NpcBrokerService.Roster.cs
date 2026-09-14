using System.Linq;
using System.Threading.Tasks;
using UKSF.Api.ArmaServer.Npc.Models;

namespace UKSF.Api.ArmaServer.Npc.Services;

// Speaker-identity half of the broker: when a player introduces themselves, every earlier
// appearance of their old label — in this NPC's history and in every other session in the
// mission, overheard included — is rewritten to their name, so a speaker never shows as
// two people in one transcript.
public partial class NpcBrokerService
{
    /// Older entries may still carry a bare UID from before the speaker was labelled;
    /// normalise oldest-first so the prompt never shows the same person two ways.
    private void NormaliseSpeakers(string sessionId, DomainNpcSession session)
    {
        foreach (var entry in session.History.Where(h => h.Role == "player" && long.TryParse(h.Speaker, out _)))
        {
            entry.Speaker = NpcPlayerRoster.DisplayName(sessionId, entry.Speaker);
        }
    }

    private async Task RewriteSpeakerAsync(string sessionId, string speakerId, string oldDisplay, string newName)
    {
        foreach (var doc in sessionsContext.Get(x => x.SessionId == sessionId).ToList())
        {
            var changed = false;
            foreach (var entry in doc.History)
            {
                if (entry.Speaker == oldDisplay || entry.Speaker == speakerId)
                {
                    entry.Speaker = newName;
                    changed = true;
                }
            }

            if (changed)
            {
                await sessionsContext.Replace(doc);
            }
        }
    }
}
