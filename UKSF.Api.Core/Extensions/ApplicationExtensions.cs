using UKSF.Api.Core.Models;

namespace UKSF.Api.Core.Extensions;

public static class ApplicationExtensions
{
    public static ApplicationAge ToAge(this DateTime dob, DateTime? date = null)
    {
        var today = date ?? DateTime.UtcNow.Date;
        var months = today.Month - dob.Month;
        var years = today.Year - dob.Year;

        if (today.Day < dob.Day)
        {
            months--;
        }

        if (months < 0)
        {
            years--;
            months += 12;
        }

        return new ApplicationAge { Years = years, Months = months };
    }

    public static bool IsAcceptableAge(this ApplicationAge age, int acceptableAge)
    {
        return age.Years >= acceptableAge || (age.Years == acceptableAge - 1 && age.Months == 1);
    }
}
