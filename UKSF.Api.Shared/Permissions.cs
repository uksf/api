using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;

namespace UKSF.Api.Shared
{
    public static class Permissions
    {
        public static readonly HashSet<string> All = new() { Member, Admin, Command, Nco, Recruiter, RecruiterLead, Personnel, Servers, Tester };

        #region MemberStates

        public const string Confirmed = "CONFIRMED";
        public const string Discharged = "DISCHARGED";
        public const string Member = "MEMBER";
        public const string Unconfirmed = "UNCONFIRMED";

        #endregion

        #region Roles

        public const string Superadmin = "SUPERADMIN";
        public const string Admin = "ADMIN";
        public const string Command = "COMMAND";
        public const string Nco = "NCO";
        public const string Recruiter = "RECRUITER";
        public const string RecruiterLead = "RECRUITER_LEAD";
        public const string Personnel = "PERSONNEL";
        public const string Servers = "SERVERS";
        public const string Tester = "TESTER";

        #endregion
    }

    public class PermissionsAttribute : AuthorizeAttribute
    {
        public PermissionsAttribute(params string[] roles)
        {
            Roles = string.Join(",", roles.Distinct());
        }
    }
}
