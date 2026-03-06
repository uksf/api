using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Models.Request;
using UKSF.Api.Models.Response;
using UKSF.Api.Services;

namespace UKSF.Api.Controllers;

[Route("application/analytics")]
public class ApplicationAnalyticsController(
    IApplicationFunnelEventContext context,
    IBotDetectionService botDetectionService,
    IAnalyticsRateLimiter rateLimiter,
    IApplicationFunnelService funnelService
) : ControllerBase
{
    [HttpPost("event")]
    [AllowAnonymous]
    public async Task<IActionResult> TrackEvent([FromBody] TrackFunnelEventRequest request)
    {
        var userAgent = Request.Headers.UserAgent.ToString();

        if (botDetectionService.IsBot(userAgent) || rateLimiter.IsRateLimited(request.VisitorId))
        {
            return Ok();
        }

        await context.Add(
            new DomainApplicationFunnelEvent
            {
                VisitorId = request.VisitorId,
                Event = request.Event,
                Value = request.Value,
                Timestamp = DateTime.UtcNow,
                UserAgent = userAgent
            }
        );

        return Ok();
    }

    [HttpGet("funnel")]
    [Permissions(Permissions.Recruiter)]
    public ApplicationFunnelResponse GetFunnel()
    {
        return funnelService.GetFunnel();
    }
}
