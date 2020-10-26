using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;

namespace UKSF.Api.Auth.Services {
    public interface ILoginService {
        string Login(string email, string password);
        string LoginForPasswordReset(string email);
        string RegenerateBearerToken(string accountId);
    }

    public class LoginService : ILoginService {
        private readonly IAccountService accountService;
        private readonly IPermissionsService permissionsService;

        public LoginService(IAccountService accountService, IPermissionsService permissionsService) {
            this.accountService = accountService;
            this.permissionsService = permissionsService;
        }

        public string Login(string email, string password) {
            Account account = AuthenticateAccount(email, password);
            return GenerateBearerToken(account);
        }

        public string LoginForPasswordReset(string email) {
            Account account = AuthenticateAccount(email, "", true);
            return GenerateBearerToken(account);
        }

        public string RegenerateBearerToken(string accountId) => GenerateBearerToken(accountService.Data.GetSingle(accountId));

        private Account AuthenticateAccount(string email, string password, bool passwordReset = false) {
            Account account = accountService.Data.GetSingle(x => string.Equals(x.email, email, StringComparison.InvariantCultureIgnoreCase));
            if (account != null && (passwordReset || BCrypt.Net.BCrypt.Verify(password, account.password))) {
                return account;
            }

            throw new LoginFailedException();
        }

        private string GenerateBearerToken(Account account) {
            List<Claim> claims = new List<Claim> { new Claim(ClaimTypes.Email, account.email, ClaimValueTypes.String), new Claim(ClaimTypes.Sid, account.id, ClaimValueTypes.String) };
            claims.AddRange(permissionsService.GrantPermissions(account).Select(x => new Claim(ClaimTypes.Role, x)));

            return JsonConvert.ToString(
                new JwtSecurityTokenHandler().WriteToken(
                    new JwtSecurityToken(
                        ApiAuthExtensions.TokenIssuer,
                        ApiAuthExtensions.TokenAudience,
                        claims,
                        DateTime.UtcNow,
                        DateTime.UtcNow.AddDays(15),
                        new SigningCredentials(ApiAuthExtensions.SecurityKey, SecurityAlgorithms.HmacSha256)
                    )
                )
            );
        }
    }

    public class LoginFailedException : Exception { }
}
