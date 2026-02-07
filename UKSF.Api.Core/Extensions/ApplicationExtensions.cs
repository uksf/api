using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Core.Extensions;

public static class ApplicationExtensions
{
    extension(DateTime dob)
    {
        public ApplicationAge ToAge(DateTime? date = null)
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
    }

    extension(ApplicationAge age)
    {
        public bool IsAcceptableAge(int acceptableAge)
        {
            return age.Years >= acceptableAge || (age.Years == acceptableAge - 1 && age.Months == 1);
        }
    }
}
