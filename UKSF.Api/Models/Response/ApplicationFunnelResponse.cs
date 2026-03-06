namespace UKSF.Api.Models.Response;

public class ApplicationFunnelResponse
{
    public FunnelData LastMonth { get; set; }
    public FunnelData Total { get; set; }
}

public class FunnelData
{
    public FunnelStages Stages { get; set; }
    public FunnelDurations AvgDuration { get; set; }
}

public class FunnelStages
{
    public int InfoPageViews { get; set; }
    public int InfoPageNext { get; set; }
    public int AccountCreated { get; set; }
    public int EmailConfirmed { get; set; }
    public int CommsLinked { get; set; }
    public int ApplicationSubmitted { get; set; }
}

public class FunnelDurations
{
    public double Overall { get; set; }
    public double Bounced { get; set; }
    public double Proceeded { get; set; }
}
