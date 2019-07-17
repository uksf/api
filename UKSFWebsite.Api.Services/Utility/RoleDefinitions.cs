using Microsoft.AspNetCore.Authorization;

namespace UKSFWebsite.Api.Services.Utility {
    public static class RoleDefinitions {
        public const string ADMIN = "ADMIN";
        public const string COMMAND = "COMMAND";
        public const string CONFIRMED = "CONFIRMED";
        public const string DISCHARGED = "DISCHARGED";
        public const string MEMBER = "MEMBER";
        public const string NCO = "NCO";
        public const string SR1 = "SR1";
        public const string SR5 = "SR5";
        public const string SR10 = "SR10";
        public const string SR1_LEAD = "SR1_LEAD";
        public const string UNCONFIRMED = "UNCONFIRMED";
    }

    public class RolesAttribute : AuthorizeAttribute {
        public RolesAttribute(params string[] roles) => Roles = string.Join(",", roles);
    }
}
