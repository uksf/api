﻿using System;

namespace UKSF.Api.Base.Extensions {
    public static class DateExtensions {
        public static (int years, int months) ToAge(this DateTime dob, DateTime? date = null) {
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

            return (years, months);
        }
    }
}