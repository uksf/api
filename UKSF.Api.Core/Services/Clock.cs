namespace UKSF.Api.Core.Services;

public interface IClock
{
    public DateTime Now();
    public DateTime Today();
    public DateTime UtcNow();
    public DateTime UkNow();
    public DateTime UkToday();
    public DateTime NextUkHourUtc(DateTime previous, params int[] ukHours);
}

public class Clock : IClock
{
    private static readonly TimeZoneInfo UkTimeZone = TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");

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
        return TimeZoneInfo.ConvertTime(UtcNow(), UkTimeZone);
    }

    public DateTime UkToday()
    {
        return UkNow().Date;
    }

    public DateTime NextUkHourUtc(DateTime previous, params int[] ukHours)
    {
        if (ukHours.Length == 0)
        {
            throw new ArgumentException("At least one UK hour must be provided", nameof(ukHours));
        }

        var previousUk = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(previous, DateTimeKind.Utc), UkTimeZone);
        var sortedHours = ukHours.OrderBy(h => h).ToArray();
        var nextHour = sortedHours.Cast<int?>().FirstOrDefault(h => h > previousUk.Hour) ?? sortedHours[0] + 24;
        return TimeZoneInfo.ConvertTimeToUtc(previousUk.Date.AddHours(nextHour), UkTimeZone);
    }
}
