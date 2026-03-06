using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Models.Response;

namespace UKSF.Api.Services;

public interface IApplicationFunnelService
{
    ApplicationFunnelResponse GetFunnel();
}

public class ApplicationFunnelService(IApplicationFunnelEventContext context) : IApplicationFunnelService
{
    public ApplicationFunnelResponse GetFunnel()
    {
        var allEvents = context.Get().ToList();
        var cutoff = DateTime.UtcNow.AddDays(-30);
        var lastMonthEvents = allEvents.Where(e => e.Timestamp >= cutoff).ToList();

        return new ApplicationFunnelResponse { LastMonth = BuildFunnelData(lastMonthEvents), Total = BuildFunnelData(allEvents) };
    }

    private static FunnelData BuildFunnelData(List<DomainApplicationFunnelEvent> events)
    {
        var visitorsThatClickedNext = events.Where(e => e.Event == "info_page_next").Select(e => e.VisitorId).ToHashSet();

        var durations = events.Where(e => e.Event == "info_page_duration" && e.Value.HasValue).ToList();
        var bouncedDurations = durations.Where(e => !visitorsThatClickedNext.Contains(e.VisitorId)).ToList();
        var proceededDurations = durations.Where(e => visitorsThatClickedNext.Contains(e.VisitorId)).ToList();

        return new FunnelData
        {
            Stages = new FunnelStages
            {
                InfoPageViews = events.Count(e => e.Event == "info_page_view"),
                InfoPageNext = events.Count(e => e.Event == "info_page_next"),
                AccountCreated = events.Count(e => e.Event == "account_created"),
                EmailConfirmed = events.Count(e => e.Event == "email_confirmed"),
                CommsLinked = events.Count(e => e.Event == "comms_linked"),
                ApplicationSubmitted = events.Count(e => e.Event == "application_submitted")
            },
            AvgDuration = new FunnelDurations
            {
                Overall = durations.Count > 0 ? Math.Round(durations.Average(e => e.Value!.Value), 2) : 0,
                Bounced = bouncedDurations.Count > 0 ? Math.Round(bouncedDurations.Average(e => e.Value!.Value), 2) : 0,
                Proceeded = proceededDurations.Count > 0 ? Math.Round(proceededDurations.Average(e => e.Value!.Value), 2) : 0
            }
        };
    }
}
