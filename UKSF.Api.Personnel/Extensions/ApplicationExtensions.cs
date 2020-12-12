using System;
using UKSF.Api.Personnel.Models;

namespace UKSF.Api.Personnel.Extensions {
    public static class ApplicationExtensions {
        public static ApplicationAge ToAge(this DateTime dob, DateTime? date = null) {
            DateTime today = date ?? DateTime.Today;
            int months = today.Month - dob.Month;
            int years = today.Year - dob.Year;

            if (today.Day < dob.Day) {
                months--;
            }

            if (months < 0) {
                years--;
                months += 12;
            }

            return new ApplicationAge { Years = years, Months = months };
        }
    }
}
