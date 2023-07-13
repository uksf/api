namespace UKSF.Api.Core.Services;

public interface IClock
{
    public DateTime Now();
    public DateTime Today();
    public DateTime UtcNow();
    public DateTime UkNow();
}

public class Clock : IClock
{
    public DateTime Now()
    {
        return DateTime.Now;
    }

    public DateTime Today()
    {
        return UtcNow().Date;
    }

    public DateTime UtcNow()
    {
        return DateTime.UtcNow;
    }

    public DateTime UkNow()
    {
        var ukZone = TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");
        return TimeZoneInfo.ConvertTime(UtcNow(), ukZone);
    }
}
