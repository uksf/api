using System;

namespace UKSFWebsite.Api.Models.Utility {
    public static class Helpers {
        public static string Value(this Enum value) => value.ToString("D");
    }
}
