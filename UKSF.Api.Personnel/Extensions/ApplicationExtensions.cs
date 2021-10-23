using System;
using UKSF.Api.Personnel.Models;

namespace UKSF.Api.Personnel.Extensions
{
    public static class ApplicationExtensions
    {
        public static ApplicationAge ToAge(this DateTime dob, DateTime? date = null)
        {
            var today = date ?? DateTime.Today;
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

            return new() { Years = years, Months = months };
        }
    }
}
