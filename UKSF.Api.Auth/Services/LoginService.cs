using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;

namespace UKSF.Api.Auth.Services {
    public interface ILoginService {
        string Login(string email, string password);
        string LoginForPasswordReset(string email);
        string RegenerateBearerToken(string accountId);
    }

    public class LoginService : ILoginService {
        private readonly IAccountContext _accountContext;
        private readonly IPermissionsService _permissionsService;

        public LoginService(IAccountContext accountContext, IPermissionsService permissionsService) {
            _accountContext = accountContext;
            _permissionsService = permissionsService;
        }

        public string Login(string email, string password) {
            Account account = AuthenticateAccount(email, password);
            return GenerateBearerToken(account);
        }

        public string LoginForPasswordReset(string email) {
            Account account = AuthenticateAccount(email, "", true);
            return GenerateBearerToken(account);
        }

        public string RegenerateBearerToken(string accountId) => GenerateBearerToken(_accountContext.GetSingle(accountId));

        private Account AuthenticateAccount(string email, string password, bool passwordReset = false) {
            Account account = _accountContext.GetSingle(x => string.Equals(x.Email, email, StringComparison.InvariantCultureIgnoreCase));
            if (account != null && (passwordReset || BCrypt.Net.BCrypt.Verify(password, account.Password))) {
                return account;
            }

            throw new LoginFailedException();
        }

        private string GenerateBearerToken(Account account) {
            List<Claim> claims = new() { new Claim(ClaimTypes.Email, account.Email, ClaimValueTypes.String), new Claim(ClaimTypes.Sid, account.Id, ClaimValueTypes.String) };
            claims.AddRange(_permissionsService.GrantPermissions(account).Select(x => new Claim(ClaimTypes.Role, x)));

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
