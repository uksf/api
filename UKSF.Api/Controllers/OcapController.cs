using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Services;

namespace UKSF.Api.Controllers;

[Route("ocap")]
[Authorize]
public class OcapController(IOcapEmbedTokenService ocapEmbedTokenService) : ControllerBase
{
    /// <summary>
    /// Short-lived OCAP JWT for the AAR iframe embed (same shape/secret as OCAP web).
    /// </summary>
    [HttpGet("embed-token")]
    public OcapEmbedTokenResponse GetEmbedToken()
    {
        return ocapEmbedTokenService.CreateForCurrentUser();
    }
}
