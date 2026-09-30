using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.ArmaServer.DataContext;

namespace UKSF.Api.ArmaServer.Controllers;

public record NpcVoiceName(string VoiceId, string DisplayName);

/// The voice picker in the Eden editor has no login, so this lists base voice IDs and names only.
[Route("npcvoices/names")]
public class NpcVoiceNamesController(INpcVoicesContext context) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public List<NpcVoiceName> GetNames()
    {
        return context.Get()
                      .Where(x => string.IsNullOrEmpty(x.MoodOf))
                      .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
                      .Select(x => new NpcVoiceName(x.VoiceId, x.DisplayName))
                      .ToList();
    }
}
