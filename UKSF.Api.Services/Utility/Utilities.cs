using System;
using Newtonsoft.Json;
using UKSF.Api.Models.Personnel;

namespace UKSF.Api.Services.Utility {
    public static class Utilities {
        private static TOut Copy<TOut>(this object source) {
            JsonSerializerSettings deserializeSettings = new JsonSerializerSettings {ObjectCreationHandling = ObjectCreationHandling.Replace};
            return JsonConvert.DeserializeObject<TOut>(JsonConvert.SerializeObject(source), deserializeSettings);
        }

        public static ExtendedAccount ToExtendedAccount(this Account account) {
            ExtendedAccount extendedAccount = account.Copy<ExtendedAccount>();
            extendedAccount.password = null;
            return extendedAccount;
        }

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
