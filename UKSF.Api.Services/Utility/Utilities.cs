using System;
using Newtonsoft.Json;
using UKSF.Api.Models.Personnel;

namespace UKSF.Api.Services.Utility {
    public static class Utilities {
        private static TOut Copy<TIn, TOut>(this TIn source) {
            JsonSerializerSettings deserializeSettings = new JsonSerializerSettings {ObjectCreationHandling = ObjectCreationHandling.Replace};
            return JsonConvert.DeserializeObject<TOut>(JsonConvert.SerializeObject(source), deserializeSettings);
        }

        public static ExtendedAccount ToExtendedAccount(this Account account) {
            ExtendedAccount extendedAccount = account.Copy<Account, ExtendedAccount>();
            extendedAccount.password = null;
            return extendedAccount;
        }

        public static (int years, int months) ToAge(this DateTime dob) {
            int months = DateTime.Today.Month - dob.Month;
            int years = DateTime.Today.Year - dob.Year;

            if (DateTime.Today.Day < dob.Day) {
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
