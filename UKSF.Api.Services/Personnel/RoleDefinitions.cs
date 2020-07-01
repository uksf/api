using System.Linq;
using Microsoft.AspNetCore.Authorization;

namespace UKSF.Api.Services.Personnel {
    public static class RoleDefinitions {
        #region MemberStates

        public const string CONFIRMED = "CONFIRMED";
        public const string DISCHARGED = "DISCHARGED";
        public const string MEMBER = "MEMBER";
        public const string UNCONFIRMED = "UNCONFIRMED";

        #endregion

        #region Roles

        public const string ADMIN = "ADMIN";
        public const string COMMAND = "COMMAND";
        public const string NCO = "NCO";
        public const string RECRUITER = "RECRUITER";
        public const string RECRUITER_LEAD = "RECRUITER_LEAD";
        public const string PERSONNEL = "PERSONNEL";
        public const string SERVERS = "SERVERS";
        public const string TESTER = "TESTER";

        #endregion
    }

    public class RolesAttribute : AuthorizeAttribute {
        public RolesAttribute(params string[] roles) => Roles = string.Join(",", roles.Distinct());
    }
}
