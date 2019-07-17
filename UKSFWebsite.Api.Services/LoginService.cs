using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using UKSFWebsite.Api.Models.Accounts;
using UKSFWebsite.Api.Services.Abstraction;
using UKSFWebsite.Api.Services.Utility;

namespace UKSFWebsite.Api.Services {
    public class LoginService : ILoginService {
        private readonly IAccountService accountService;

        private readonly string[] admins = {"59e38f10594c603b78aa9dbd", "5a1e894463d0f71710089106", "5a1ae0f0b9bcb113a44edada"};
        private readonly IRanksService ranksService;
        private readonly IRecruitmentService recruitmentService;
        private readonly IUnitsService unitsService;
        private bool isPasswordReset;

        public LoginService(IAccountService accountService, IRanksService ranksService, IUnitsService unitsService, IRecruitmentService recruitmentService) {
            this.accountService = accountService;
            this.ranksService = ranksService;
            this.unitsService = unitsService;
            this.recruitmentService = recruitmentService;
            isPasswordReset = false;
        }

        public static SymmetricSecurityKey SecurityKey { get; set; }
        public static string TokenAudience { private get; set; }
        public static string TokenIssuer { private get; set; }

        public string Login(string email, string password) {
            Account account = FindAccount(email, password);
            return GenerateToken(account);
        }

        public string LoginWithoutPassword(string email) {
            isPasswordReset = true;
            Account account = FindAccount(email, "");
            return GenerateToken(account);
        }

        public string RegenerateToken(string accountId) => GenerateToken(accountService.GetSingle(accountId));

        private Account FindAccount(string email, string password) {
            Account account = accountService.GetSingle(x => string.Equals(x.email, email, StringComparison.InvariantCultureIgnoreCase));
            if (account != null) {
                if (!isPasswordReset) {
                    if (!BCrypt.Net.BCrypt.Verify(password, account.password)) {
                        throw new LoginFailedException("Password incorrect");
                    }
                }

                return account;
            }

            throw new LoginFailedException($"No account found with email '{email}'");
        }

        private string GenerateToken(Account account) {
            List<Claim> claims = new List<Claim> {new Claim(ClaimTypes.Email, account.email, ClaimValueTypes.String), new Claim(ClaimTypes.Sid, account.id, ClaimValueTypes.String)};
            ResolveRoles(claims, account);
            return JsonConvert.ToString(new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(TokenIssuer, TokenAudience, claims, DateTime.UtcNow, DateTime.UtcNow.AddDays(15), new SigningCredentials(SecurityKey, SecurityAlgorithms.HmacSha256))));
        }

        private void ResolveRoles(ICollection<Claim> claims, Account account) {
            switch (account.membershipState) {
                case MembershipState.MEMBER: {
                    claims.Add(new Claim(ClaimTypes.Role, RoleDefinitions.MEMBER));
                    bool admin = admins.Contains(account.id);
                    if (admin) {
                        claims.Add(new Claim(ClaimTypes.Role, RoleDefinitions.ADMIN));
                    }

                    if (unitsService.MemberHasAnyRole(account.id) || admin) {
                        claims.Add(new Claim(ClaimTypes.Role, RoleDefinitions.COMMAND));
                    }

                    if (account.rank != null && ranksService.IsSuperiorOrEqual(account.rank, "Senior Aircraftman") || admin) {
                        claims.Add(new Claim(ClaimTypes.Role, RoleDefinitions.NCO));
                    }

                    if (recruitmentService.IsAccountSr1Lead(account) || admin) {
                        claims.Add(new Claim(ClaimTypes.Role, RoleDefinitions.SR1_LEAD));
                    }

                    if (recruitmentService.IsRecruiter(account) || admin) {
                        claims.Add(new Claim(ClaimTypes.Role, RoleDefinitions.SR1));
                    }

                    if (unitsService.GetSingle(x => x.shortname == "SR10").members.Contains(account.id) || admin) {
                        claims.Add(new Claim(ClaimTypes.Role, RoleDefinitions.SR10));
                    }

                    if (unitsService.GetSingle(x => x.shortname == "SR5").members.Contains(account.id) || admin) {
                        claims.Add(new Claim(ClaimTypes.Role, RoleDefinitions.SR5));
                    }

                    break;
                }

                case MembershipState.SERVER:
                    claims.Add(new Claim(ClaimTypes.Role, RoleDefinitions.ADMIN));
                    break;
                case MembershipState.CONFIRMED:
                    claims.Add(new Claim(ClaimTypes.Role, RoleDefinitions.CONFIRMED));
                    break;
                case MembershipState.DISCHARGED:
                    claims.Add(new Claim(ClaimTypes.Role, RoleDefinitions.DISCHARGED));
                    break;
                case MembershipState.UNCONFIRMED: break;
                case MembershipState.EMPTY: break;
                default:
                    claims.Add(new Claim(ClaimTypes.Role, RoleDefinitions.UNCONFIRMED));
                    break;
            }
        }
    }

    public class LoginFailedException : Exception {
        public LoginFailedException(string message) : base(message) { }
    }
}
